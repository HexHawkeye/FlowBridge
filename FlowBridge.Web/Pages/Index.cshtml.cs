using FlowBridge.Core.Models;
using FlowBridge.Core.Services;
using FlowBridge.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace FlowBridge.Web.Pages;

public sealed class IndexModel(FlowBridgeDbContext db, IIntegrationRunner runner) : PageModel
{
    public List<IntegrationDefinition> Integrations { get; private set; } = [];
    public List<ExecutionRecord> RecentExecutions { get; private set; } = [];
    [TempData] public string? Notice { get; set; }

    public async Task OnGetAsync()
    {
        Integrations = await db.Integrations.OrderBy(x => x.Name).ToListAsync();
        RecentExecutions = await db.Executions.Include(x => x.IntegrationDefinition)
            .OrderByDescending(x => x.StartedUtc).Take(20).ToListAsync();
    }

    public async Task<IActionResult> OnPostRunAsync(int id, CancellationToken cancellationToken)
    {
        try
        {
            var result = await runner.RunAsync(id, cancellationToken: cancellationToken);
            Notice = result.Succeeded
                ? $"Run #{result.Id} completed successfully in {result.DurationMs} ms."
                : $"Run #{result.Id} failed: {result.ErrorMessage}";
        }
        catch (Exception ex)
        {
            Notice = $"The integration could not start: {ex.Message}";
        }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostToggleAsync(int id)
    {
        var integration = await db.Integrations.FindAsync(id);
        if (integration is null) return NotFound();
        integration.IsEnabled = !integration.IsEnabled;
        integration.UpdatedUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var integration = await db.Integrations.FindAsync(id);
        if (integration is null) return NotFound();

        db.Integrations.Remove(integration);
        await db.SaveChangesAsync();
        Notice = $"Integration '{integration.Name}' and its execution history were deleted.";
        return RedirectToPage();
    }
}
