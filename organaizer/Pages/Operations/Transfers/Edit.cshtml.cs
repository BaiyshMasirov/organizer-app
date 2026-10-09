using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using organaizer.Domain;
using organaizer.Infrastructure;

namespace organaizer.Pages.Operations.Transfers;

public sealed class EditModel(FinanceDbContext db, ActiveCompany active) : PageModel
{
    [BindProperty] public TransferForm Input { get; set; } = new();
    public Guid Id { get; private set; }
    public List<MoneyAccount> Accounts { get; private set; } = [];
    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        Id=id;
        var operation=await db.Operations.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==id && x.CompanyId==active.RequiredId && x.TypeCode==TransferService.TypeCode);
        if(operation is null) return NotFound();
        if(operation.Status==OperationStatus.Cancelled) return RedirectToPage("Details",new { id });
        var rows=await db.AccountMovements.AsNoTracking().Where(x=>x.CompanyId==active.RequiredId && x.GroupId==id).ToListAsync();
        if(rows.Count!=2 || rows.Count(x=>x.Amount<0)!=1 || rows.Count(x=>x.Amount>0)!=1) return BadRequest();
        Input=new TransferForm { FromAccountId=rows.Single(x=>x.Amount<0).AccountId,ToAccountId=rows.Single(x=>x.Amount>0).AccountId,
            Amount=operation.SellAmount,OccurredAt=operation.OccurredAt.ToOffset(TimeSpan.FromHours(5)).Date,
            Note=operation.Note,Revision=rows[0].Revision };
        await LoadAsync();return Page();
    }
    public async Task<IActionResult> OnPostAsync(Guid id)
    {
        Id=id;
        if(!await db.Operations.AnyAsync(x=>x.Id==id && x.CompanyId==active.RequiredId && x.TypeCode==TransferService.TypeCode)) return NotFound();
        if(ModelState.IsValid)
        {
            try
            {
                await new TransferService(db).UpdateAsync(active.RequiredId,id,Input.Revision,Input.FromAccountId,Input.ToAccountId,
                    Input.Amount,Input.OccurredAt,Input.Note,User.Identity?.Name??"system");
                TempData["Message"]="Перевод изменён, остатки пересчитаны";
                return RedirectToPage("Details",new { id });
            }
            catch(ArgumentException ex) { ModelState.AddModelError("",ex.Message); }
            catch(DbUpdateException) { ModelState.AddModelError("","Перевод уже изменён. Обновите страницу перед сохранением."); }
        }
        await LoadAsync();return Page();
    }
    private async Task LoadAsync() => Accounts=await db.Accounts.AsNoTracking().Include(x=>x.FinancialInstitution)
        .Where(x=>x.CompanyId==active.RequiredId && (x.IsActive || x.Id==Input.FromAccountId || x.Id==Input.ToAccountId))
        .OrderBy(x=>x.Name).ThenBy(x=>x.Currency).ToListAsync();
}
