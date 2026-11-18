using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using organaizer.Domain;
using organaizer.Infrastructure;

namespace organaizer.Pages.Operations.Loans;

public sealed class IndexModel(FinanceDbContext db, ActiveCompany active) : PageModel
{
    [BindProperty(SupportsGet = true)] public Guid? CounterpartyId { get; set; }
    [BindProperty(SupportsGet = true)] public string? Currency { get; set; }
    [BindProperty(SupportsGet = true)] public string State { get; set; } = "open";
    public List<SelectListItem> Counterparties { get; private set; } = [];
    public List<SelectListItem> Currencies { get; private set; } = [];
    public List<LoanSummary> Summaries { get; private set; } = [];
    public List<TradeOperation> Movements { get; private set; } = [];

    public sealed record LoanSummary(Guid CounterpartyId, string Counterparty, string Currency,
        decimal Received, decimal Repaid, decimal Outstanding, DateTimeOffset LastMovement, DateTimeOffset? DueAt);

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await db.Companies.AsNoTracking().AnyAsync(x => x.Id == active.RequiredId && x.Kind == CompanyKind.LiquidityProvider))
            return NotFound();
        Counterparties = await db.Counterparties.AsNoTracking().Where(x => x.CompanyId == active.RequiredId && x.IsActive)
            .OrderBy(x => x.Name).Select(x => new SelectListItem(x.Name, x.Id.ToString())).ToListAsync();
        Currencies = await db.Currencies.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Code)
            .Select(x => new SelectListItem(x.Code, x.Code)).ToListAsync();

        var query = db.Operations.AsNoTracking().Include(x => x.Counterparty)
            .Where(x => x.CompanyId == active.RequiredId && x.Status != OperationStatus.Cancelled &&
                (x.TypeCode == LoanService.ReceivedTypeCode || x.TypeCode == LoanService.RepaidTypeCode));
        if (CounterpartyId.HasValue) query = query.Where(x => x.CounterpartyId == CounterpartyId);
        if (!string.IsNullOrWhiteSpace(Currency))
        {
            Currency = Currency.Trim().ToUpperInvariant();
            query = query.Where(x => x.BuyCurrency == Currency);
        }

        Movements = await query.OrderByDescending(x => x.OccurredAt).ThenByDescending(x => x.OperationNumber).ToListAsync();
        Summaries = Movements.GroupBy(x => new
            {
                CounterpartyId = x.CounterpartyId ?? Guid.Empty,
                Counterparty = x.Counterparty?.Name ?? "Без контрагента",
                Currency = x.BuyCurrency
            })
            .Select(group => new LoanSummary(group.Key.CounterpartyId, group.Key.Counterparty, group.Key.Currency,
                group.Where(x => x.TypeCode == LoanService.ReceivedTypeCode).Sum(x => x.BuyAmount),
                group.Where(x => x.TypeCode == LoanService.RepaidTypeCode).Sum(x => x.SellAmount),
                group.Where(x => x.TypeCode == LoanService.ReceivedTypeCode).Sum(x => x.BuyAmount) -
                    group.Where(x => x.TypeCode == LoanService.RepaidTypeCode).Sum(x => x.SellAmount),
                group.Max(x => x.OccurredAt),
                group.Where(x => x.TypeCode == LoanService.ReceivedTypeCode && x.DueAt.HasValue)
                    .Min(x => x.DueAt)))
            .Where(x => State != "open" || x.Outstanding != 0)
            .OrderByDescending(x => x.Outstanding != 0).ThenBy(x => x.Counterparty).ThenBy(x => x.Currency).ToList();
        return Page();
    }
}
