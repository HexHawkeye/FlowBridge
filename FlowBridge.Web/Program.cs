using FlowBridge.Core.Services;
using FlowBridge.Core.Models;
using FlowBridge.Infrastructure.Data;
using FlowBridge.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole();
builder.Services.AddRazorPages(options =>
{
    options.Conventions.AllowAnonymousToPage("/Login");
});
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Login";
        options.LogoutPath = "/Logout";
        options.Cookie.Name = ".FlowBridge.Admin";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
    });
builder.Services.AddAuthorization(options =>
    options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(
    builder.Configuration["DataProtection:KeyPath"] ?? Path.Combine(builder.Environment.ContentRootPath, "keys")));
builder.Services.AddHealthChecks();
builder.Services.AddDbContext<FlowBridgeDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("FlowBridge")));
builder.Services.AddHttpClient("FlowBridge", client => client.Timeout = TimeSpan.FromSeconds(60));
builder.Services.AddScoped<IIntegrationRunner, IntegrationRunner>();
builder.Services.AddHostedService<ScheduledIntegrationWorker>();
builder.Services.AddHostedService<RetryWorker>();

var app = builder.Build();
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
    app.UseHttpsRedirection();
}
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapPost("/integrations/save", async (HttpRequest request, FlowBridgeDbContext db) =>
{
    var form = await request.ReadFormAsync();
    var id = int.TryParse(form["Id"], out var parsedId) ? parsedId : 0;
    var name = form["Name"].ToString().Trim();
    var method = form["Method"].ToString().Trim().ToUpperInvariant();
    var url = form["Url"].ToString().Trim();
    var headersJson = form["HeadersJson"].ToString().Trim();

    if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(url))
        return Results.BadRequest("Name and endpoint URL are required.");

    if (!Uri.TryCreate(url, UriKind.Absolute, out var endpoint) ||
        endpoint.Scheme is not ("http" or "https"))
        return Results.BadRequest("The endpoint must be a valid HTTP or HTTPS URL.");

    try
    {
        _ = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(
            string.IsNullOrWhiteSpace(headersJson) ? "{}" : headersJson);
    }
    catch (System.Text.Json.JsonException)
    {
        return Results.BadRequest("Headers must be a JSON object containing string values.");
    }

    var integration = id == 0
        ? new IntegrationDefinition { CreatedUtc = DateTime.UtcNow }
        : await db.Integrations.FindAsync(id);

    if (integration is null)
        return Results.NotFound("Integration not found.");

    integration.Name = name;
    integration.Description = form["Description"].ToString().Trim();
    integration.ConnectorType = form["ConnectorType"].ToString() == "GraphQL" ? "GraphQL" : "REST";
    integration.Method = string.IsNullOrWhiteSpace(method) ? "GET" : method;
    integration.Url = url;
    integration.HeadersJson = string.IsNullOrWhiteSpace(headersJson) ? "{}" : headersJson;
    var secretHeadersJson = form["SecretHeadersJson"].ToString().Trim();
    if (!string.IsNullOrWhiteSpace(secretHeadersJson) && secretHeadersJson != "{}")
    {
        try { _ = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(secretHeadersJson); }
        catch (System.Text.Json.JsonException) { return Results.BadRequest("Secret headers must be a JSON object containing string values."); }
        var protector = app.Services.GetRequiredService<IDataProtectionProvider>().CreateProtector("FlowBridge.SecretHeaders.v1");
        integration.ProtectedHeadersJson = protector.Protect(secretHeadersJson);
    }
    integration.BodyTemplate = form["BodyTemplate"].ToString();
    integration.VariablesJson = string.IsNullOrWhiteSpace(form["VariablesJson"]) ? "{}" : form["VariablesJson"].ToString().Trim();
    integration.GraphQlQuery = form["GraphQlQuery"].ToString();
    integration.GraphQlVariablesJson = string.IsNullOrWhiteSpace(form["GraphQlVariablesJson"]) ? "{}" : form["GraphQlVariablesJson"].ToString().Trim();
    integration.GraphQlOperationName = form["GraphQlOperationName"].ToString().Trim();
    integration.IsEnabled = form.ContainsKey("IsEnabled");
    integration.ScheduleEnabled = form.ContainsKey("ScheduleEnabled");
    integration.IntervalMinutes = int.TryParse(form["IntervalMinutes"], out var interval) ? Math.Clamp(interval, 1, 525600) : 15;
    integration.MaxRetries = int.TryParse(form["MaxRetries"], out var retries) ? Math.Clamp(retries, 0, 10) : 3;
    integration.RetryDelaySeconds = int.TryParse(form["RetryDelaySeconds"], out var retryDelay) ? Math.Clamp(retryDelay, 5, 86400) : 30;
    if (integration.ScheduleEnabled && (integration.NextRunUtc is null || integration.NextRunUtc <= DateTime.UtcNow)) integration.NextRunUtc = DateTime.UtcNow.AddMinutes(integration.IntervalMinutes);
    if (!integration.ScheduleEnabled) integration.NextRunUtc = null;
    integration.UpdatedUtc = DateTime.UtcNow;

    if (id == 0)
        db.Integrations.Add(integration);

    await db.SaveChangesAsync();
    return Results.Redirect("/");
}).DisableAntiforgery();

app.MapRazorPages();
app.MapHealthChecks("/health").AllowAnonymous();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<FlowBridgeDbContext>();
    await DatabaseUpgrade.ApplyAsync(db);
}

app.Run();
