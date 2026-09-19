using FlowBridge.Core.Models;
using FlowBridge.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace FlowBridge.Web.Pages.Executions;

public sealed class DetailsModel(FlowBridgeDbContext db, FlowBridge.Core.Services.IIntegrationRunner runner) : PageModel
{
    public ExecutionRecord Execution { get; private set; } = null!;
    public async Task<IActionResult> OnGetAsync(long id)
    {
        var result = await db.Executions.Include(x => x.IntegrationDefinition).SingleOrDefaultAsync(x => x.Id == id);
        if (result is null) return NotFound();
        Execution = result; return Page();
    }

    public async Task<IActionResult> OnPostRetryAsync(long id, CancellationToken cancellationToken)
    {
        var failed = await db.Executions.FindAsync([id], cancellationToken);
        if (failed is null) return NotFound();
        await runner.RunAsync(failed.IntegrationDefinitionId, 1, failed.RetryOfExecutionId ?? failed.Id, cancellationToken);
        return RedirectToPage("/Index");
    }
}
