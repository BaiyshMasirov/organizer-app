using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using organaizer.Domain;
using organaizer.Infrastructure;

namespace organaizer.Pages.Balance;

public sealed class IndexModel(FinanceDbContext db, ActiveCompany active) : PageModel
{
    public List<MoneyAccount> Items { get; private set; } = [];
    public List<SelectListItem> Institutions { get; private set; } = [];
    public List<SelectListItem> Currencies { get; private set; } = [];
    public Dictionary<Guid, decimal> Balances { get; private set; } = [];
    public List<FinancialInstitution> ConversionBanks { get; private set; } = [];
    public Domain.Company ActiveCompany { get; private set; } = null!;

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public sealed class InputModel
    {
        [Required(ErrorMessage = "Выберите кошелек или банк")]
        public Guid? FinancialInstitutionId { get; set; }

        [Required(ErrorMessage = "Выберите валюту"), StringLength(5)]
        public string Currency { get; set; } = "USDT";

        [Range(typeof(decimal), "-999999999999999", "999999999999999", ErrorMessage = "Укажите корректную сумму")]
        public decimal Amount { get; set; }
    }

    public async Task OnGetAsync() => await LoadAsync();

    public async Task<IActionResult> OnPostAsync()
    {
        var institution = Input.FinancialInstitutionId.HasValue
            ? await db.FinancialInstitutions.SingleOrDefaultAsync(x => x.Id == Input.FinancialInstitutionId && x.IsActive)
            : null;
        if (institution is null) ModelState.AddModelError("Input.FinancialInstitutionId", "Выберите существующий кошелек или банк");

        Input.Currency = Input.Currency.Trim().ToUpperInvariant();
        if (!await db.Currencies.AnyAsync(x => x.Code == Input.Currency && x.IsActive))
            ModelState.AddModelError("Input.Currency", "Выберите активную валюту");
        if (institution is not null && await db.Accounts.AnyAsync(x => x.FinancialInstitutionId == institution.Id && x.Currency == Input.Currency))
            ModelState.AddModelError(string.Empty, "Для этого источника и валюты баланс уже создан. Измените существующую запись.");
        if (!ModelState.IsValid) { await LoadAsync(); return Page(); }

        var companyId = active.RequiredId;
        db.Accounts.Add(new MoneyAccount
        {
            Id = Guid.NewGuid(), CompanyId = companyId, FinancialInstitutionId = institution!.Id,
            Name = institution.Name, Kind = ToAccountKind(institution.Kind), Currency = Input.Currency,
            OpeningBalance = Input.Amount
        });
        await db.SaveChangesAsync();
        TempData["Success"] = "Баланс добавлен";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostUpdateAsync(Guid id, decimal amount)
    {
        var account = await db.Accounts.SingleOrDefaultAsync(x => x.Id == id && x.CompanyId == active.RequiredId);
        if (account is null) return NotFound();
        account.OpeningBalance = amount;
        await db.SaveChangesAsync();
        TempData["Success"] = "Сумма обновлена";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostTransferAsync(Guid fromAccountId, Guid toAccountId, decimal amount, string? note)
    {
        var accounts = await db.Accounts.Where(x => x.CompanyId == active.RequiredId && (x.Id == fromAccountId || x.Id == toAccountId)).ToListAsync();
        var from = accounts.SingleOrDefault(x => x.Id == fromAccountId); var to = accounts.SingleOrDefault(x => x.Id == toAccountId);
        if (from is null || to is null || from.Id == to.Id || from.Currency != to.Currency || amount <= 0)
            return BalanceError("Выберите два разных счета в одной валюте и укажите сумму.");
        var available = await BalanceCalculator.GetAsync(db, from.Id);
        if (available < amount) return Insufficient(from.Name, available, amount, from.Currency);
        await AddMovementPair(from, to, amount, amount, AccountMovementKind.Transfer, note);
        TempData["Success"] = "Перевод выполнен"; return RedirectToPage();
    }

    public async Task<IActionResult> OnPostConvertAsync(Guid financialInstitutionId, Guid fromAccountId, Guid toAccountId, decimal fromAmount, decimal toAmount, string? note)
    {
        var accounts = await db.Accounts.Where(x => x.CompanyId == active.RequiredId && (x.Id == fromAccountId || x.Id == toAccountId)).ToListAsync();
        var from = accounts.SingleOrDefault(x => x.Id == fromAccountId); var to = accounts.SingleOrDefault(x => x.Id == toAccountId);
        if (from is null || to is null || from.Id == to.Id || from.FinancialInstitutionId != financialInstitutionId || to.FinancialInstitutionId != financialInstitutionId || from.Currency == to.Currency || fromAmount <= 0 || toAmount <= 0)
            return BalanceError("Для конвертации выберите две разные валюты в одном банке.");
        var available = await BalanceCalculator.GetAsync(db, from.Id);
        if (available < fromAmount) return Insufficient(from.Name, available, fromAmount, from.Currency);
        await AddMovementPair(from, to, fromAmount, toAmount, AccountMovementKind.Conversion, note);
        TempData["Success"] = $"Конвертация {from.Currency} → {to.Currency} выполнена"; return RedirectToPage();
    }

    private async Task AddMovementPair(MoneyAccount from, MoneyAccount to, decimal fromAmount, decimal toAmount, AccountMovementKind kind, string? note)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(); var groupId = Guid.NewGuid(); var now = DateTimeOffset.UtcNow;
        db.AccountMovements.AddRange(
            new AccountMovement { Id=Guid.NewGuid(), CompanyId=active.RequiredId, AccountId=from.Id, GroupId=groupId, Kind=kind, OccurredAt=now, Amount=-fromAmount, Currency=from.Currency, Note=note?.Trim() },
            new AccountMovement { Id=Guid.NewGuid(), CompanyId=active.RequiredId, AccountId=to.Id, GroupId=groupId, Kind=kind, OccurredAt=now, Amount=toAmount, Currency=to.Currency, Note=note?.Trim() });
        await db.SaveChangesAsync(); await transaction.CommitAsync();
    }

    private IActionResult Insufficient(string account, decimal available, decimal requested, string currency)
    { TempData["BalanceError"] = $"На счете «{account}» не хватает средств. Доступно {available:N2} {currency}, требуется {requested:N2} {currency}."; return RedirectToPage(); }
    private IActionResult BalanceError(string message) { TempData["BalanceError"] = message; return RedirectToPage(); }

    private async Task LoadAsync()
    {
        ActiveCompany = (await active.GetAsync())!;
        Items = await db.Accounts.AsNoTracking().Include(x => x.FinancialInstitution)
            .Where(x => x.IsActive && x.CompanyId == active.RequiredId).OrderBy(x => x.FinancialInstitution!.Name).ThenBy(x => x.Currency).ToListAsync();
        foreach (var item in Items) Balances[item.Id] = await BalanceCalculator.GetAsync(db, item.Id);
        ConversionBanks = Items.Where(x=>x.FinancialInstitution?.Kind==InstitutionKind.Bank&&x.FinancialInstitutionId.HasValue).GroupBy(x=>x.FinancialInstitutionId).Where(g=>g.Select(x=>x.Currency).Distinct().Count()>1).Select(g=>g.First().FinancialInstitution!).OrderBy(x=>x.Name).ToList();
        Institutions = await db.FinancialInstitutions.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name)
            .Select(x => new SelectListItem(x.Name + " · " + KindLabel(x.Kind), x.Id.ToString())).ToListAsync();
        Currencies = await db.Currencies.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Code)
            .Select(x => new SelectListItem(x.Code + " — " + x.Name, x.Code)).ToListAsync();
    }

    public static string KindLabel(InstitutionKind kind) => kind switch
    {
        InstitutionKind.Bank => "Банк", InstitutionKind.Exchange => "Биржа", InstitutionKind.Wallet => "Кошелек",
        InstitutionKind.PaymentSystem => "Платежная система", _ => "Другое"
    };

    private static AccountKind ToAccountKind(InstitutionKind kind) => kind switch
    {
        InstitutionKind.Bank => AccountKind.Bank, InstitutionKind.Exchange => AccountKind.Exchange,
        InstitutionKind.Wallet => AccountKind.CryptoWallet, _ => AccountKind.Cash
    };
}
