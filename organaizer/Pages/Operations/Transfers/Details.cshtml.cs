using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using organaizer.Domain;
using organaizer.Infrastructure;

namespace organaizer.Pages.Operations.Transfers;

public sealed class DetailsModel(FinanceDbContext db, ActiveCompany active) : PageModel
{
    public TradeOperation Transfer { get; private set; } = null!;
    public int Revision { get; private set; }
    public List<TransferRevision> History { get; private set; } = [];
    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var operation=await db.Operations.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==id && x.CompanyId==active.RequiredId && x.TypeCode==TransferService.TypeCode);
        if(operation is null) return NotFound();
        Transfer=operation;
        Revision=await db.AccountMovements.Where(x=>x.GroupId==id && x.CompanyId==active.RequiredId).MaxAsync(x=>(int?)x.Revision)??0;
        History=await db.TransferRevisions.AsNoTracking().Where(x=>x.TransferId==id && x.CompanyId==active.RequiredId).OrderByDescending(x=>x.Revision).ToListAsync();
        return Page();
    }
    public async Task<IActionResult> OnPostCancelAsync(Guid id,int revision)
    {
        if(!await db.Operations.AnyAsync(x=>x.Id==id && x.CompanyId==active.RequiredId && x.TypeCode==TransferService.TypeCode)) return NotFound();
        try { await new TransferService(db).CancelAsync(active.RequiredId,id,revision,User.Identity?.Name??"system");TempData["Message"]="Перевод отменён. Его влияние на остатки убрано."; }
        catch(ArgumentException ex) { TempData["TransferError"]=ex.Message; }
        catch(DbUpdateException) { TempData["TransferError"]="Перевод уже изменён. Обновите страницу."; }
        return RedirectToPage(new { id });
    }
}
