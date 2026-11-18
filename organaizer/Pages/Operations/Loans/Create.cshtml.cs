using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using organaizer.Domain;
using organaizer.Infrastructure;

namespace organaizer.Pages.Operations.Loans;

public sealed class CreateModel(FinanceDbContext db, ActiveCompany active) : PageModel
{
    [BindProperty(SupportsGet = true)] public string? ReturnUrl { get; set; }
    public string ReturnTarget => Url.IsLocalUrl(ReturnUrl) ? ReturnUrl! : "/Operations/Loans";
    [BindProperty] public InputModel Input { get; set; } = new();
    public List<SelectListItem> Counterparties { get; private set; } = [];
    public List<MoneyAccount> Accounts { get; private set; } = [];

    public sealed class InputModel
    {
        [Required] public string TypeCode { get; set; } = LoanService.ReceivedTypeCode;
        [Required] public Guid CounterpartyId { get; set; }
        [Required] public Guid AccountId { get; set; }
        [Range(typeof(decimal), "0.00000001", "9999999999999999")] public decimal Amount { get; set; }
        public DateTime OccurredAt { get; set; } = DateTime.Today;
        public DateTime? DueAt { get; set; }
        [MaxLength(500)] public string? Note { get; set; }
    }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await IsAaAsync()) return NotFound();
        await LoadAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!await IsAaAsync()) return NotFound();
        if (ModelState.IsValid)
        {
            try
            {
                await new LoanService(db).CreateAsync(active.RequiredId, Input.CounterpartyId, Input.AccountId,
                    Input.TypeCode, Input.Amount, Input.OccurredAt, Input.DueAt, Input.Note);
                TempData["Message"] = Input.TypeCode == LoanService.ReceivedTypeCode
                    ? "Получение займа сохранено и зачислено на баланс"
                    : "Возврат займа сохранён и списан с баланса";
                return LocalRedirect(ReturnTarget);
            }
            catch (ArgumentException ex) { ModelState.AddModelError(string.Empty, ex.Message); }
        }
        await LoadAsync();
        return Page();
    }

    private async Task LoadAsync()
    {
        Counterparties = await db.Counterparties.AsNoTracking().Where(x => x.CompanyId == active.RequiredId && x.IsActive)
            .OrderBy(x => x.Name).Select(x => new SelectListItem(x.Name, x.Id.ToString())).ToListAsync();
        Accounts = await db.Accounts.AsNoTracking().Include(x => x.FinancialInstitution)
            .Where(x => x.CompanyId == active.RequiredId && x.IsActive).OrderBy(x => x.Name).ThenBy(x => x.Currency).ToListAsync();
    }

    private Task<bool> IsAaAsync() => db.Companies.AsNoTracking().AnyAsync(x =>
        x.Id == active.RequiredId && x.Kind == CompanyKind.LiquidityProvider);
}
