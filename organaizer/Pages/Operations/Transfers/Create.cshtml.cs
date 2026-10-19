using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using organaizer.Domain;
using organaizer.Infrastructure;

namespace organaizer.Pages.Operations.Transfers;

public sealed class CreateModel(FinanceDbContext db, ActiveCompany active) : PageModel
{
    [BindProperty] public TransferForm Input { get; set; } = new();
    public List<MoneyAccount> Accounts { get; private set; } = [];
    public bool IsCopy { get; private set; }
    public async Task<IActionResult> OnGetAsync(Guid? copyFrom = null)
    {
        if(copyFrom.HasValue)
        {
            var source=await db.Operations.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==copyFrom.Value &&
                x.CompanyId==active.RequiredId && x.TypeCode==TransferService.TypeCode);
            if(source is null) return NotFound();
            var movements=await db.AccountMovements.AsNoTracking().Where(x=>x.GroupId==source.Id && x.CompanyId==active.RequiredId).ToListAsync();
            if(movements.Count!=2 || movements.Count(x=>x.Amount<0)!=1 || movements.Count(x=>x.Amount>0)!=1) return BadRequest();
            Input=new TransferForm { FromAccountId=movements.Single(x=>x.Amount<0).AccountId,
                ToAccountId=movements.Single(x=>x.Amount>0).AccountId,Amount=source.SellAmount,Note=source.Note };
            IsCopy=true;
        }
        await LoadAsync();return Page();
    }
    public async Task<IActionResult> OnPostAsync()
    {
        if(ModelState.IsValid)
        {
            try
            {
                var id=await new TransferService(db).CreateAsync(active.RequiredId,Input.FromAccountId,Input.ToAccountId,
                    Input.Amount,Input.OccurredAt,Input.Note,User.Identity?.Name??"system");
                TempData["Message"]="Перевод сохранён";
                return RedirectToPage("Details",new { id });
            }
            catch(ArgumentException ex) { ModelState.AddModelError("",ex.Message); }
        }
        await LoadAsync();return Page();
    }
    private async Task LoadAsync() => Accounts=await db.Accounts.AsNoTracking().Include(x=>x.FinancialInstitution)
        .Where(x=>x.CompanyId==active.RequiredId && (x.IsActive || x.Id==Input.FromAccountId || x.Id==Input.ToAccountId)).OrderBy(x=>x.Name).ThenBy(x=>x.Currency).ToListAsync();
}
