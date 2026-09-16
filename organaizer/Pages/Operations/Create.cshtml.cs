using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using organaizer.Application;
using organaizer.Domain;
using organaizer.Infrastructure;

namespace organaizer.Pages.Operations;

public sealed class CreateModel(Dispatcher dispatcher, FinanceDbContext db, ActiveCompany active) : PageModel
{
    [BindProperty] public InputModel Input { get; set; } = new();
    [BindProperty] public bool ConfirmInsufficientFunds { get; set; }
    public bool IsCopy { get; private set; }
    public bool IsLiquidityProvider { get; private set; }
    public List<SelectListItem> Companies { get; private set; } = [];
    public List<SelectListItem> Counterparties { get; private set; } = [];
    public List<SelectListItem> Currencies { get; private set; } = [];
    public List<MoneyAccount> AccountItems { get; private set; } = [];
    public Dictionary<Guid, decimal> AccountBalances { get; private set; } = [];
    public string? BalanceError { get; private set; }
    public IReadOnlyDictionary<string, string> Types => OperationTypes.All;

    public sealed class InputModel
    {
        [Required] public Guid CompanyId { get; set; }
        public Guid? CounterpartyId { get; set; }
        [Required] public string TypeCode { get; set; } = "BUY_USDT_USD";
        public DateTime OccurredAt { get; set; } = DateTime.Today;
        public DateTime? DueAt { get; set; }
        [Required] public string SellCurrency { get; set; } = "USD";
        [Range(typeof(decimal), "0", "9999999999999999")] public decimal SellAmount { get; set; }
        [Required] public string BuyCurrency { get; set; } = "USDT";
        [Range(typeof(decimal), "0.00000001", "9999999999999999")] public decimal BuyAmount { get; set; }
        public decimal? ExchangeRate { get; set; }
        public decimal FeeAmount { get; set; }
        public string FeeCurrency { get; set; } = "USD";
        public decimal BaseCurrencyProfit { get; set; }
        public Guid? SellAccountId { get; set; }
        public Guid? BuyAccountId { get; set; }
        public string? Note { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(Guid? copyFrom)
    {
        if (!copyFrom.HasValue) { await Load(); return Page(); }
        var source = await db.Operations.AsNoTracking().Include(x => x.Settlements)
            .SingleOrDefaultAsync(x => x.Id == copyFrom && x.CompanyId == active.RequiredId);
        if (source is null) return NotFound();
        var sellSettlement = source.Settlements.Where(x => x.Amount < 0).OrderBy(x => x.OccurredAt).FirstOrDefault();
        var buySettlement = source.Settlements.Where(x => x.Amount > 0).OrderBy(x => x.OccurredAt).FirstOrDefault();
        Input = new InputModel
        {
            CompanyId=active.RequiredId, CounterpartyId=source.CounterpartyId, TypeCode=source.TypeCode,
            OccurredAt=source.OccurredAt.Date, DueAt=source.DueAt?.Date, SellCurrency=source.SellCurrency,
            SellAmount=source.SellAmount, BuyCurrency=source.BuyCurrency, BuyAmount=source.BuyAmount,
            ExchangeRate=source.ExchangeRate, FeeAmount=source.FeeAmount, FeeCurrency=source.FeeCurrency,
            BaseCurrencyProfit=source.BaseCurrencyProfit, SellAccountId=sellSettlement?.AccountId,
            BuyAccountId=buySettlement?.AccountId, Note=source.Note
        };
        await Load();
        Input.SellAccountId ??= FindImportedAccount(source.SourceAccount, source.SellCurrency);
        Input.BuyAccountId ??= FindImportedAccount(source.DestinationAccount, source.BuyCurrency);
        IsCopy = true;
        return Page();
    }

    public async Task<JsonResult> OnGetRateAsync(string sellCurrency, string buyCurrency, DateTime date)
    {
        var rate = await AaExchangeRateService.PairRateAsync(db, sellCurrency, buyCurrency, date);
        return new JsonResult(new
        {
            found = rate.HasValue,
            rate,
            source = sellCurrency.Equals("RUB", StringComparison.OrdinalIgnoreCase) || buyCurrency.Equals("RUB", StringComparison.OrdinalIgnoreCase)
                ? "ЦБ РФ" : "справочник A&A"
        });
    }

    public async Task<JsonResult> OnGetProfitRateAsync(string currency, DateTime date)
    {
        var rate = await NbkrRateService.RateToUsdAsync(db, currency, date);
        return new JsonResult(new { found=rate.HasValue, rate, source="НБКР" });
    }

    public async Task<IActionResult> OnPostAsync()
    {
        Input.CompanyId = active.RequiredId;
        ModelState.Remove("Input.CompanyId");
        var companyKind = await db.Companies.AsNoTracking().Where(x => x.Id == active.RequiredId)
            .Select(x => (CompanyKind?)x.Kind).SingleOrDefaultAsync();
        var oneSidedIncome = OperationTypes.IsOneSidedIncome(Input.TypeCode);

        if (oneSidedIncome)
        {
            Input.SellAmount = 0;
            Input.SellCurrency = Input.BuyCurrency;
            Input.SellAccountId = null;
            Input.ExchangeRate = 1m;
            ModelState.Remove("Input.SellAmount");
            ModelState.Remove("Input.SellCurrency");
            ModelState.Remove("Input.SellAccountId");
            ModelState.Remove("Input.ExchangeRate");
            if (!Input.BuyAccountId.HasValue)
                ModelState.AddModelError("Input.BuyAccountId", "Выберите счёт, на который поступило вознаграждение");
        }
        else if (Input.SellAmount <= 0)
        {
            ModelState.AddModelError("Input.SellAmount", "Сумма должна быть больше нуля");
        }

        if (companyKind == CompanyKind.LiquidityProvider && !oneSidedIncome && (!Input.ExchangeRate.HasValue || Input.ExchangeRate <= 0))
        {
            Input.ExchangeRate = await AaExchangeRateService.PairRateAsync(db, Input.SellCurrency, Input.BuyCurrency, Input.OccurredAt);
            ModelState.Remove("Input.ExchangeRate");
            if (!Input.ExchangeRate.HasValue)
                ModelState.AddModelError("Input.ExchangeRate", "Обменный курс на дату операции не найден");
        }

        if (companyKind == CompanyKind.Broker && Input.FeeAmount != 0)
        {
            var nbkrRate = await NbkrRateService.RateToUsdAsync(db, Input.FeeCurrency, Input.OccurredAt);
            if (nbkrRate.HasValue)
            {
                Input.ExchangeRate = nbkrRate;
                Input.BaseCurrencyProfit = Math.Round(Input.FeeAmount * nbkrRate.Value, 2);
                ModelState.Remove("Input.ExchangeRate");
                ModelState.Remove("Input.BaseCurrencyProfit");
            }
            else ModelState.AddModelError("Input.ExchangeRate", $"Курс НБКР для {Input.FeeCurrency} на дату операции не найден.");
        }
        if (companyKind == CompanyKind.Broker && Input.OccurredAt.Date >= new DateTime(2026, 9, 1) && Input.BaseCurrencyProfit == 0)
            ModelState.AddModelError("Input.BaseCurrencyProfit", "Для операций Orient Capital с 01.09.2026 прибыль USD не может быть равна нулю.");
        if (Input.CounterpartyId.HasValue && !await db.Counterparties.AnyAsync(x => x.Id == Input.CounterpartyId && x.CompanyId == active.RequiredId))
            ModelState.AddModelError("Input.CounterpartyId", "Клиент не принадлежит компании");

        var ids = new[] { Input.SellAccountId, Input.BuyAccountId }.Where(x => x.HasValue).Select(x => x!.Value).ToList();
        var selected = await db.Accounts.Include(x => x.FinancialInstitution)
            .Where(x => x.CompanyId == active.RequiredId && ids.Contains(x.Id)).ToListAsync();
        var sell = Input.SellAccountId.HasValue ? selected.SingleOrDefault(x => x.Id == Input.SellAccountId) : null;
        var buy = Input.BuyAccountId.HasValue ? selected.SingleOrDefault(x => x.Id == Input.BuyAccountId) : null;
        if (Input.SellAccountId.HasValue && (sell is null || sell.Currency != Input.SellCurrency))
            ModelState.AddModelError("Input.SellAccountId", "Счёт списания должен быть в валюте операции");
        if (Input.BuyAccountId.HasValue && (buy is null || buy.Currency != Input.BuyCurrency))
            ModelState.AddModelError("Input.BuyAccountId", "Счёт зачисления должен быть в валюте операции");
        if (sell is not null && Input.SellAmount > 0)
        {
            var available = await BalanceCalculator.GetAsync(db, sell.Id);
            if (available < Input.SellAmount && !ConfirmInsufficientFunds)
                BalanceError = $"На счёте «{sell.Name}» доступно {available:N2} {sell.Currency}, а нужно {Input.SellAmount:N2} {sell.Currency}. Вы уверены, что хотите сохранить операцию?";
        }
        if (!ModelState.IsValid || BalanceError is not null) { await Load(); return Page(); }

        var i = Input;
        await using var tx = await db.Database.BeginTransactionAsync();
        var operationId = await dispatcher.Send(new CreateOperationCommand(active.RequiredId, i.CounterpartyId, i.TypeCode,
            new DateTimeOffset(i.OccurredAt, TimeSpan.Zero), i.DueAt.HasValue ? new DateTimeOffset(i.DueAt.Value, TimeSpan.Zero) : null,
            i.SellCurrency, i.SellAmount, i.BuyCurrency, i.BuyAmount, i.FeeAmount, i.FeeCurrency,
            i.BaseCurrencyProfit, i.ExchangeRate, sell?.Name, buy?.Name, i.Note));
        if (sell is not null && i.SellAmount > 0)
            await dispatcher.Send(new AddSettlementCommand(operationId, sell.Id, new DateTimeOffset(i.OccurredAt, TimeSpan.Zero), -i.SellAmount, i.SellCurrency, "Списание по операции"));
        if (buy is not null)
            await dispatcher.Send(new AddSettlementCommand(operationId, buy.Id, new DateTimeOffset(i.OccurredAt, TimeSpan.Zero), i.BuyAmount, i.BuyCurrency, "Зачисление по операции"));
        await tx.CommitAsync();
        return RedirectToPage("Index");
    }

    private async Task Load()
    {
        Input.CompanyId = active.RequiredId;
        IsLiquidityProvider = await db.Companies.AsNoTracking().AnyAsync(x => x.Id == active.RequiredId && x.Kind == CompanyKind.LiquidityProvider);
        Companies = await db.Companies.AsNoTracking().Where(x => x.Id == active.RequiredId).Select(x => new SelectListItem(x.Name, x.Id.ToString())).ToListAsync();
        Counterparties = await db.Counterparties.AsNoTracking().Where(x => x.IsActive && x.CompanyId == active.RequiredId).OrderBy(x => x.Name).Select(x => new SelectListItem(x.Name, x.Id.ToString())).ToListAsync();
        Currencies = await db.Currencies.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Code).Select(x => new SelectListItem(x.Code + " — " + x.Name, x.Code)).ToListAsync();
        AccountItems = await db.Accounts.AsNoTracking().Include(x => x.FinancialInstitution)
            .Where(x => x.IsActive && x.CompanyId == active.RequiredId).OrderBy(x => x.Name).ThenBy(x => x.Currency).ToListAsync();
        foreach (var account in AccountItems) AccountBalances[account.Id] = await BalanceCalculator.GetAsync(db, account.Id);
    }

    private Guid? FindImportedAccount(string? name, string currency) => AccountItems.FirstOrDefault(x => x.Currency == currency &&
        (string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase) || string.Equals(x.FinancialInstitution?.Name, name, StringComparison.OrdinalIgnoreCase)))?.Id;
}
