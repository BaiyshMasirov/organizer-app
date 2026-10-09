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
    public async Task OnGetAsync() => await LoadAsync();
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
        .Where(x=>x.CompanyId==active.RequiredId && x.IsActive).OrderBy(x=>x.Name).ThenBy(x=>x.Currency).ToListAsync();
}
