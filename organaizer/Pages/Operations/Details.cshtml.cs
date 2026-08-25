using Microsoft.AspNetCore.Mvc; using Microsoft.AspNetCore.Mvc.RazorPages; using Microsoft.EntityFrameworkCore; using organaizer.Application; using organaizer.Domain; using organaizer.Infrastructure;
namespace organaizer.Pages.Operations;
public sealed class DetailsModel(FinanceDbContext db,Dispatcher dispatcher):PageModel
{
 public TradeOperation Operation{get;private set;}=null!;public Domain.Company Company{get;private set;}=null!;
 public async Task<IActionResult> OnGetAsync(Guid id){var operation=await db.Operations.AsNoTracking().Include(x=>x.Counterparty).Include(x=>x.Settlements).ThenInclude(x=>x.Account).SingleOrDefaultAsync(x=>x.Id==id);if(operation is null)return NotFound();Operation=operation;Company=await db.Companies.AsNoTracking().SingleAsync(x=>x.Id==operation.CompanyId);return Page();}
 public async Task<IActionResult> OnPostCompleteAsync(Guid id){await dispatcher.Send(new CompleteOperationCommand(id));return RedirectToPage(new{id});}
 public async Task<IActionResult> OnPostCancelAsync(Guid id){await dispatcher.Send(new CancelOperationCommand(id));return RedirectToPage("Index");}
}
