using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using FlowBridge.Core.Models;
using FlowBridge.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FlowBridge.Web.Pages.Integrations;

public sealed class EditModel(FlowBridgeDbContext db) : PageModel
{
    [BindProperty] public InputModel Input { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        if (id is null) return Page();
        var item = await db.Integrations.FindAsync(id.Value);
        if (item is null) return NotFound();
        Input = new() { Id=item.Id, Name=item.Name, Description=item.Description, ConnectorType=item.ConnectorType, Method=item.Method, Url=item.Url, HeadersJson=item.HeadersJson, BodyTemplate=item.BodyTemplate, VariablesJson=item.VariablesJson, GraphQlQuery=item.GraphQlQuery, GraphQlVariablesJson=item.GraphQlVariablesJson, GraphQlOperationName=item.GraphQlOperationName, IsEnabled=item.IsEnabled, ScheduleEnabled=item.ScheduleEnabled, IntervalMinutes=item.IntervalMinutes, MaxRetries=item.MaxRetries, RetryDelaySeconds=item.RetryDelaySeconds };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        Input.HeadersJson = string.IsNullOrWhiteSpace(Input.HeadersJson) ? "{}" : Input.HeadersJson;
        try { JsonSerializer.Deserialize<Dictionary<string,string>>(Input.HeadersJson); }
        catch (JsonException) { ModelState.AddModelError("Input.HeadersJson", "Headers must be a JSON object containing string values."); }
        if (!ModelState.IsValid) return Page();

        var item = Input.Id == 0 ? new IntegrationDefinition() : await db.Integrations.FindAsync(Input.Id);
        if (item is null) return NotFound();
        item.Name=Input.Name; item.Description=Input.Description; item.Method=Input.Method; item.Url=Input.Url;
        item.HeadersJson=Input.HeadersJson; item.BodyTemplate=Input.BodyTemplate; item.IsEnabled=Input.IsEnabled; item.UpdatedUtc=DateTime.UtcNow;
        if (Input.Id == 0) db.Integrations.Add(item);
        await db.SaveChangesAsync();
        return RedirectToPage("/Index");
    }

    public sealed class InputModel
    {
        public int Id { get; set; }
        [Required, StringLength(150)] public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string ConnectorType { get; set; } = "REST";
        [Required] public string Method { get; set; } = "GET";
        [Required, Url] public string Url { get; set; } = "";
        public string HeadersJson { get; set; } = "{}";
        public string BodyTemplate { get; set; } = "";
        public string VariablesJson { get; set; } = "{}";
        public string GraphQlQuery { get; set; } = "";
        public string GraphQlVariablesJson { get; set; } = "{}";
        public string GraphQlOperationName { get; set; } = "";
        public bool IsEnabled { get; set; } = true;
        public bool ScheduleEnabled { get; set; }
        [Range(1, 525600)] public int IntervalMinutes { get; set; } = 15;
        [Range(0, 10)] public int MaxRetries { get; set; } = 3;
        [Range(5, 86400)] public int RetryDelaySeconds { get; set; } = 30;
    }
}
