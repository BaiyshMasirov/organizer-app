using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using organaizer.Domain;
using organaizer.Infrastructure;

namespace organaizer.Pages.Clients;

public sealed class ReconciliationModel(FinanceDbContext db, ActiveCompany active) : PageModel
{
    public Counterparty Client { get; private set; } = null!;
    public Domain.Company Company { get; private set; } = null!;
    public List<SelectListItem> Currencies { get; private set; } = [];
    public List<ReconciliationLine> Lines { get; private set; } = [];
    public decimal OpeningBalance { get; private set; }
    public decimal ClosingBalance { get; private set; }
    public decimal OpeningUsd { get; private set; }
    public decimal ClosingUsd { get; private set; }
    [BindProperty(SupportsGet = true)] public Guid Id { get; set; }
    [BindProperty(SupportsGet = true)] public DateTime From { get; set; } = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    [BindProperty(SupportsGet = true)] public DateTime To { get; set; } = DateTime.Today;
    [BindProperty(SupportsGet = true)] public string Currency { get; set; } = "USD";

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await LoadAsync()) return NotFound();
        var error = ValidateFilters();
        if (error is not null) { ModelState.AddModelError(string.Empty, error); return Page(); }
        await BuildPreviewAsync();
        return Page();
    }

    public async Task<IActionResult> OnGetExportAsync()
    {
        if (!await LoadAsync()) return NotFound();
        var error = ValidateFilters();
        if (error is not null) return BadRequest(error);
        await BuildPreviewAsync();
        var bytes = ReconciliationActExcel.Create(Company.Name, Client.Name, Currency, From.Date, To.Date,
            OpeningBalance >= 0 ? OpeningBalance : 0, OpeningBalance < 0 ? -OpeningBalance : 0, OpeningUsd, Lines);
        var safeName = string.Concat(Client.Name.Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch));
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"Акт сверки {safeName} {From:yyyy-MM-dd}_{To:yyyy-MM-dd} {Currency}.xlsx");
    }

    private string? ValidateFilters()
    {
        Currency = Currency.Trim().ToUpperInvariant();
        if (From.Date > To.Date) return "Начальная дата не может быть позже конечной.";
        if (!Currencies.Any(x => x.Value == Currency)) return "Неизвестная валюта.";
        return null;
    }

    private async Task BuildPreviewAsync()
    {
        var from = new DateTimeOffset(From.Date, TimeSpan.Zero);
        var toExclusive = new DateTimeOffset(To.Date.AddDays(1), TimeSpan.Zero);
        var operations = await db.Operations.AsNoTracking()
            .Where(x => x.CompanyId == active.RequiredId && x.CounterpartyId == Id && x.Status != OperationStatus.Cancelled && x.OccurredAt < toExclusive && (x.BuyCurrency == Currency || x.SellCurrency == Currency))
            .OrderBy(x => x.OccurredAt).ThenBy(x => x.Id).ToListAsync();

        decimal opening = 0, openingUsd = 0;
        var lines = new List<ReconciliationLine>();
        foreach (var operation in operations)
        {
            var type = OperationTypes.All.GetValueOrDefault(operation.TypeCode, operation.TypeCode);
            if (operation.BuyCurrency == Currency && operation.BuyAmount > 0)
            {
                var usd = await UsdEquivalentAsync(operation, true);
                if (operation.OccurredAt < from) { opening += operation.BuyAmount; openingUsd += usd; }
                else lines.Add(new ReconciliationLine(operation.Id, operation.OccurredAt,
                    $"{type} · получено {operation.BuyAmount:N2} {operation.BuyCurrency}, отдано {operation.SellAmount:N2} {operation.SellCurrency}",
                    operation.BuyAmount, 0, usd));
            }
            if (operation.SellCurrency == Currency && operation.SellAmount > 0)
            {
                var usd = await UsdEquivalentAsync(operation, false);
                if (operation.OccurredAt < from) { opening -= operation.SellAmount; openingUsd -= usd; }
                else lines.Add(new ReconciliationLine(operation.Id, operation.OccurredAt,
                    $"{type} · отдано {operation.SellAmount:N2} {operation.SellCurrency}, получено {operation.BuyAmount:N2} {operation.BuyCurrency}",
                    0, operation.SellAmount, -usd));
            }
        }
        OpeningBalance = opening;
        OpeningUsd = openingUsd;
        Lines = lines;
        ClosingBalance = opening + lines.Sum(x => x.Debit - x.Credit);
        ClosingUsd = openingUsd + lines.Sum(x => x.UsdEquivalent);
    }

    private async Task<decimal> UsdEquivalentAsync(TradeOperation operation, bool selectedIsBuy)
    {
        var amount = selectedIsBuy ? operation.BuyAmount : operation.SellAmount;
        var currency = selectedIsBuy ? operation.BuyCurrency : operation.SellCurrency;
        var pairedAmount = selectedIsBuy ? operation.SellAmount : operation.BuyAmount;
        var pairedCurrency = selectedIsBuy ? operation.SellCurrency : operation.BuyCurrency;
        if (currency is "USD" or "USDT") return amount;
        if (pairedCurrency is "USD" or "USDT") return pairedAmount;
        var rate = Company.Kind == CompanyKind.Broker
            ? await NbkrRateService.RateToUsdAsync(db, currency, operation.OccurredAt.Date)
            : await AaExchangeRateService.RateToUsdAsync(db, currency, operation.OccurredAt.Date);
        return rate.HasValue ? Math.Round(amount * rate.Value, 2) : 0;
    }

    private async Task<bool> LoadAsync()
    {
        Company = (await active.GetAsync())!;
        Client = await db.Counterparties.AsNoTracking().SingleOrDefaultAsync(x => x.Id == Id && x.CompanyId == active.RequiredId) ?? null!;
        Currencies = await db.Currencies.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Code)
            .Select(x => new SelectListItem(x.Code + " — " + x.Name, x.Code)).ToListAsync();
        return Client is not null;
    }
}
