using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using organaizer.Application;
using organaizer.Domain;
using organaizer.Infrastructure;

namespace organaizer.Pages.Operations;

public sealed class EditModel(Dispatcher dispatcher, FinanceDbContext db, ActiveCompany active) : PageModel
{
    [BindProperty] public InputModel Input { get; set; } = new();
    public List<SelectListItem> Companies { get; private set; } = [];
    public List<SelectListItem> Counterparties { get; private set; } = [];
    public List<SelectListItem> Currencies { get; private set; } = [];
    public List<MoneyAccount> AccountItems { get; private set; } = [];
    public bool IsLiquidityProvider { get; private set; }
    public IReadOnlyDictionary<string, string> Types => OperationTypes.All;

    public sealed class InputModel
    {
        public Guid Id { get; set; }
        [Required] public Guid CompanyId { get; set; }
        public Guid? CounterpartyId { get; set; }
        [Required] public string TypeCode { get; set; } = "";
        public DateTime OccurredAt { get; set; }
        public DateTime? DueAt { get; set; }
        [Required] public string SellCurrency { get; set; } = "";
        [Range(typeof(decimal), "0", "9999999999999999")] public decimal SellAmount { get; set; }
        [Required] public string BuyCurrency { get; set; } = "";
        [Range(typeof(decimal), "0.00000001", "9999999999999999")] public decimal BuyAmount { get; set; }
        public decimal FeeAmount { get; set; }
        [Required] public string FeeCurrency { get; set; } = "";
        public decimal? ExchangeRate { get; set; }
        public decimal BaseCurrencyProfit { get; set; }
        public Guid? SellAccountId { get; set; }
        public Guid? BuyAccountId { get; set; }
        public string? Note { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var x = await db.Operations.AsNoTracking().Include(x => x.Settlements).SingleOrDefaultAsync(x => x.Id == id);
        if (x is null) return NotFound();
        await Load();
        Input = new InputModel
        {
            Id=x.Id, CompanyId=x.CompanyId, CounterpartyId=x.CounterpartyId, TypeCode=x.TypeCode,
            OccurredAt=x.OccurredAt.DateTime, DueAt=x.DueAt?.DateTime, SellCurrency=x.SellCurrency,
            SellAmount=x.SellAmount, BuyCurrency=x.BuyCurrency, BuyAmount=x.BuyAmount, FeeAmount=x.FeeAmount,
            FeeCurrency=x.FeeCurrency, ExchangeRate=x.ExchangeRate, BaseCurrencyProfit=x.BaseCurrencyProfit,
            SellAccountId=x.Settlements.Where(s => s.Amount < 0).OrderBy(s => s.OccurredAt).Select(s => (Guid?)s.AccountId).FirstOrDefault(),
            BuyAccountId=x.Settlements.Where(s => s.Amount > 0).OrderBy(s => s.OccurredAt).Select(s => (Guid?)s.AccountId).FirstOrDefault(),
            Note=x.Note
        };
        Input.SellAccountId ??= FindAccount(x.SourceAccount, x.SellCurrency);
        Input.BuyAccountId ??= FindAccount(x.DestinationAccount, x.BuyCurrency);
        return Page();
    }

    public async Task<JsonResult> OnGetRateAsync(string sellCurrency, string buyCurrency, DateTime date)
    {
        var rate = await AaExchangeRateService.PairRateAsync(db, sellCurrency, buyCurrency, date);
        return new JsonResult(new
        {
            found=rate.HasValue, rate,
            source=sellCurrency.Equals("RUB", StringComparison.OrdinalIgnoreCase) || buyCurrency.Equals("RUB", StringComparison.OrdinalIgnoreCase) ? "ЦБ РФ" : "справочник A&A"
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
        var companyKind = await db.Companies.AsNoTracking().Where(x => x.Id == active.RequiredId).Select(x => (CompanyKind?)x.Kind).SingleOrDefaultAsync();
        var oneSidedIncome = OperationTypes.IsOneSidedIncome(Input.TypeCode);
        if (oneSidedIncome)
        {
            Input.SellAmount = 0;
            Input.SellCurrency = Input.BuyCurrency;
            Input.SellAccountId = null;
            Input.ExchangeRate = 1m;
            foreach (var key in new[] { "Input.SellAmount", "Input.SellCurrency", "Input.SellAccountId", "Input.ExchangeRate" }) ModelState.Remove(key);
            if (!Input.BuyAccountId.HasValue) ModelState.AddModelError("Input.BuyAccountId", "Выберите счёт, на который поступило вознаграждение");
        }
        else if (Input.SellAmount <= 0) ModelState.AddModelError("Input.SellAmount", "Сумма должна быть больше нуля");

        if (companyKind == CompanyKind.LiquidityProvider && !oneSidedIncome && (!Input.ExchangeRate.HasValue || Input.ExchangeRate <= 0))
        {
            Input.ExchangeRate = await AaExchangeRateService.PairRateAsync(db, Input.SellCurrency, Input.BuyCurrency, Input.OccurredAt);
            ModelState.Remove("Input.ExchangeRate");
            if (!Input.ExchangeRate.HasValue) ModelState.AddModelError("Input.ExchangeRate", "Обменный курс на дату операции не найден");
        }
        if (companyKind == CompanyKind.Broker && Input.FeeAmount != 0)
        {
            var rate = await NbkrRateService.RateToUsdAsync(db, Input.FeeCurrency, Input.OccurredAt);
            if (rate.HasValue)
            {
                Input.ExchangeRate = rate;
                Input.BaseCurrencyProfit = Math.Round(Input.FeeAmount * rate.Value, 2);
                ModelState.Remove("Input.ExchangeRate");
                ModelState.Remove("Input.BaseCurrencyProfit");
            }
            else ModelState.AddModelError("Input.ExchangeRate", $"Курс НБКР для {Input.FeeCurrency} на дату операции не найден.");
        }
        if (companyKind == CompanyKind.Broker && Input.OccurredAt.Date >= new DateTime(2026, 9, 1) && Input.BaseCurrencyProfit == 0)
            ModelState.AddModelError("Input.BaseCurrencyProfit", "Для операций Orient Capital с 01.09.2026 прибыль USD не может быть равна нулю.");

        var ids = new[] { Input.SellAccountId, Input.BuyAccountId }.Where(x => x.HasValue).Select(x => x!.Value).ToList();
        var accounts = await db.Accounts.Where(x => x.CompanyId == active.RequiredId && ids.Contains(x.Id)).ToListAsync();
        var sell = Input.SellAccountId.HasValue ? accounts.SingleOrDefault(x => x.Id == Input.SellAccountId) : null;
        var buy = Input.BuyAccountId.HasValue ? accounts.SingleOrDefault(x => x.Id == Input.BuyAccountId) : null;
        if (Input.SellAccountId.HasValue && (sell is null || sell.Currency != Input.SellCurrency)) ModelState.AddModelError("Input.SellAccountId", "Счёт списания должен быть в валюте операции");
        if (Input.BuyAccountId.HasValue && (buy is null || buy.Currency != Input.BuyCurrency)) ModelState.AddModelError("Input.BuyAccountId", "Счёт зачисления должен быть в валюте операции");
        if (!ModelState.IsValid) { await Load(); return Page(); }

        var current = await db.Operations.AsNoTracking().SingleOrDefaultAsync(x => x.Id == Input.Id);
        if (current is null) return NotFound();
        var i = Input;
        await dispatcher.Send(new UpdateOperationCommand(i.Id, active.RequiredId, i.CounterpartyId, i.TypeCode,
            new DateTimeOffset(i.OccurredAt, TimeSpan.Zero), i.DueAt.HasValue ? new DateTimeOffset(i.DueAt.Value, TimeSpan.Zero) : null,
            i.SellCurrency, i.SellAmount, i.BuyCurrency, i.BuyAmount, i.FeeAmount, i.FeeCurrency,
            i.BaseCurrencyProfit, i.ExchangeRate, current.Status, sell?.Name, buy?.Name, i.Note));
        return RedirectToPage("Details", new { id=i.Id });
    }

    private async Task Load()
    {
        IsLiquidityProvider = await db.Companies.AsNoTracking().AnyAsync(x => x.Id == active.RequiredId && x.Kind == CompanyKind.LiquidityProvider);
        Companies = await db.Companies.AsNoTracking().Where(x => x.Id == active.RequiredId).Select(x => new SelectListItem(x.Name, x.Id.ToString())).ToListAsync();
        Counterparties = await db.Counterparties.AsNoTracking().Where(x => x.IsActive && x.CompanyId == active.RequiredId).OrderBy(x => x.Name).Select(x => new SelectListItem(x.Name, x.Id.ToString())).ToListAsync();
        Currencies = await db.Currencies.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Code).Select(x => new SelectListItem(x.Code + " — " + x.Name, x.Code)).ToListAsync();
        AccountItems = await db.Accounts.AsNoTracking().Include(x => x.FinancialInstitution).Where(x => x.IsActive && x.CompanyId == active.RequiredId).OrderBy(x => x.Name).ThenBy(x => x.Currency).ToListAsync();
    }

    private Guid? FindAccount(string? name, string currency) => AccountItems.FirstOrDefault(x => x.Currency == currency &&
        (string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase) || string.Equals(x.FinancialInstitution?.Name, name, StringComparison.OrdinalIgnoreCase)))?.Id;
}
