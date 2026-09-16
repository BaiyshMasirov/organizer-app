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
    [BindProperty(SupportsGet=true)] public Guid Id { get; set; }
    [BindProperty(SupportsGet=true)] public DateTime From { get; set; } = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    [BindProperty(SupportsGet=true)] public DateTime To { get; set; } = DateTime.Today;
    [BindProperty(SupportsGet=true)] public string Currency { get; set; } = "USD";

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await LoadAsync()) return NotFound();
        return Page();
    }

    public async Task<IActionResult> OnGetExportAsync()
    {
        if (!await LoadAsync()) return NotFound();
        Currency = Currency.Trim().ToUpperInvariant();
        if (From.Date > To.Date) return BadRequest("Начальная дата не может быть позже конечной.");
        if (!Currencies.Any(x => x.Value == Currency)) return BadRequest("Неизвестная валюта.");

        var from = new DateTimeOffset(From.Date, TimeSpan.Zero);
        var toExclusive = new DateTimeOffset(To.Date.AddDays(1), TimeSpan.Zero);
        var operations = await db.Operations.AsNoTracking()
            .Where(x => x.CompanyId == active.RequiredId && x.CounterpartyId == Id && x.Status != OperationStatus.Cancelled && x.OccurredAt < toExclusive)
            .OrderBy(x => x.OccurredAt).ThenBy(x => x.Id).ToListAsync();

        decimal openingDebit = 0, openingCredit = 0;
        var lines = new List<ReconciliationLine>();
        foreach (var operation in operations)
        {
            var type = OperationTypes.All.GetValueOrDefault(operation.TypeCode, operation.TypeCode);
            if (operation.OccurredAt < from)
            {
                if (operation.BuyCurrency == Currency) openingDebit += operation.BuyAmount;
                if (operation.SellCurrency == Currency) openingCredit += operation.SellAmount;
                continue;
            }
            if (operation.BuyCurrency == Currency && operation.BuyAmount > 0)
                lines.Add(new ReconciliationLine(operation.OccurredAt, $"Входящий актив · {type}", operation.BuyAmount, 0));
            if (operation.SellCurrency == Currency && operation.SellAmount > 0)
                lines.Add(new ReconciliationLine(operation.OccurredAt, $"Передано контрагенту · {type}", 0, operation.SellAmount));
        }

        var opening = openingDebit - openingCredit;
        var bytes = ReconciliationActExcel.Create(Company.Name, Client.Name, Currency, From.Date, To.Date,
            opening >= 0 ? opening : 0, opening < 0 ? -opening : 0, lines);
        var safeName = string.Concat(Client.Name.Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch));
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"Акт сверки {safeName} {From:yyyy-MM-dd}_{To:yyyy-MM-dd} {Currency}.xlsx");
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
