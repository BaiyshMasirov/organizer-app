using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using organaizer.Domain;
using organaizer.Infrastructure;

namespace organaizer.Pages.Balance;

public sealed class HistoryModel(FinanceDbContext db, ActiveCompany active) : PageModel
{
    private static readonly TimeSpan AlmatyOffset = TimeSpan.FromHours(5);

    public sealed record HistoryRow(Guid AccountId, string Account, string Currency, DateTimeOffset OccurredAt,
        string Type, string Description, decimal Amount, decimal Balance);

    private sealed record RawEntry(Guid AccountId, string Account, string Currency, decimal OpeningBalance,
        DateTimeOffset OccurredAt, string Type, string Description, decimal Amount);

    [BindProperty(SupportsGet = true)] public Guid? AccountId { get; set; }
    [BindProperty(SupportsGet = true)] public string? Type { get; set; }
    [BindProperty(SupportsGet = true)] public DateTime? From { get; set; }
    [BindProperty(SupportsGet = true)] public DateTime? To { get; set; }
    [BindProperty(SupportsGet = true)] public string? Currency { get; set; }

    public string AccountTitle { get; private set; } = "Все счета";
    public List<HistoryRow> Items { get; private set; } = [];
    public List<string> Types { get; private set; } = [];
    public List<string> Currencies { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync()
    {
        var accounts = await db.Accounts.AsNoTracking().Include(x => x.FinancialInstitution)
            .Where(x => x.IsActive && x.CompanyId == active.RequiredId).ToListAsync();
        if (AccountId.HasValue && accounts.All(x => x.Id != AccountId.Value)) return NotFound();
        if (AccountId.HasValue)
        {
            var selected = accounts.Single(x => x.Id == AccountId.Value);
            AccountTitle = $"{selected.FinancialInstitution?.Name ?? selected.Name} · {selected.Currency}";
        }

        var accountIds = accounts.Select(x => x.Id).ToList();
        var accountMap = accounts.ToDictionary(x => x.Id);
        var settlements = await db.Settlements.AsNoTracking()
            .Where(x => accountIds.Contains(x.AccountId) && x.Operation!.Status != OperationStatus.Cancelled)
            .Select(x => new { x.AccountId, x.OccurredAt, x.Amount, x.Note, x.Operation!.TypeCode,
                Counterparty = x.Operation.Counterparty != null ? x.Operation.Counterparty.Name : null }).ToListAsync();
        var expenses = await db.Expenses.AsNoTracking().Where(x => accountIds.Contains(x.AccountId))
            .Select(x => new { x.AccountId, x.OccurredAt, x.Amount, x.Category, x.Note }).ToListAsync();
        var movements = await db.AccountMovements.AsNoTracking().Where(x => accountIds.Contains(x.AccountId))
            .Select(x => new { x.AccountId, x.OccurredAt, x.Amount, x.Kind, x.Note }).ToListAsync();

        RawEntry Create(Guid id, DateTimeOffset at, string type, string description, decimal amount)
        {
            var account = accountMap[id];
            return new RawEntry(id, account.FinancialInstitution?.Name ?? account.Name, account.Currency,
                account.OpeningBalance, at, type, description, amount);
        }

        var entries = new List<RawEntry>();
        entries.AddRange(settlements.Select(x => Create(x.AccountId, x.OccurredAt,
            OperationTypes.All.GetValueOrDefault(x.TypeCode, x.TypeCode),
            string.Join(" · ", new[] { x.Counterparty, x.Note }.Where(v => !string.IsNullOrWhiteSpace(v))), x.Amount)));
        entries.AddRange(expenses.Select(x => Create(x.AccountId, x.OccurredAt, "Расход",
            string.Join(" · ", new[] { x.Category, x.Note }.Where(v => !string.IsNullOrWhiteSpace(v))), -x.Amount)));
        entries.AddRange(movements.Select(x => Create(x.AccountId, x.OccurredAt,
            x.Kind == AccountMovementKind.Transfer ? "Перевод" : "Конвертация", x.Note ?? "", x.Amount)));

        Types = entries.Select(x => x.Type).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList();
        Currencies = accounts.Select(x => x.Currency).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList();
        Type = Type?.Trim();
        Currency = Currency?.Trim().ToUpperInvariant();

        var rows = new List<HistoryRow>();
        foreach (var group in entries.GroupBy(x => x.AccountId))
        {
            var balance = group.First().OpeningBalance;
            foreach (var entry in group.OrderBy(x => x.OccurredAt).ThenBy(x => x.Type))
            {
                balance += entry.Amount;
                rows.Add(new HistoryRow(entry.AccountId, entry.Account, entry.Currency, entry.OccurredAt,
                    entry.Type, entry.Description, entry.Amount, balance));
            }
        }

        var fromInstant = From.HasValue ? new DateTimeOffset(From.Value.Date, AlmatyOffset) : (DateTimeOffset?)null;
        var toInstant = To.HasValue ? new DateTimeOffset(To.Value.Date.AddDays(1), AlmatyOffset) : (DateTimeOffset?)null;
        Items = rows.Where(x =>
                (!AccountId.HasValue || x.AccountId == AccountId.Value) &&
                (string.IsNullOrWhiteSpace(Type) || x.Type.Equals(Type, StringComparison.OrdinalIgnoreCase)) &&
                (string.IsNullOrWhiteSpace(Currency) || x.Currency.Equals(Currency, StringComparison.OrdinalIgnoreCase)) &&
                (!fromInstant.HasValue || x.OccurredAt >= fromInstant.Value) &&
                (!toInstant.HasValue || x.OccurredAt < toInstant.Value))
            .OrderByDescending(x => x.OccurredAt).ThenBy(x => x.Account).ToList();
        return Page();
    }

    public static string LocalDate(DateTimeOffset value) => value.ToOffset(AlmatyOffset).ToString("dd.MM.yyyy HH:mm");
}
