using Microsoft.EntityFrameworkCore;
using organaizer.Domain;

namespace organaizer.Infrastructure;

public static class ExpenseValuation
{
    public static async Task<decimal?> CalculateAsync(FinanceDbContext db, Guid companyId,
        decimal amount, string currency, DateTime date, CancellationToken ct = default)
    {
        var kind = await db.Companies.Where(x => x.Id == companyId)
            .Select(x => (CompanyKind?)x.Kind).SingleOrDefaultAsync(ct);
        if (kind is null) return null;
        var rate = kind == CompanyKind.Broker
            ? await NbkrRateService.RateToUsdAsync(db, currency.Trim(), date, ct)
            : await AaExchangeRateService.RateToUsdAsync(db, currency, date, ct);
        return rate > 0 ? Math.Round(amount * rate.Value, 2) : null;
    }

    // Older forms saved zero even when the expense had a nonzero amount.
    public static async Task<decimal> ResolveAsync(FinanceDbContext db, Expense expense, CancellationToken ct = default)
        => expense.BaseCurrencyAmount != 0 ? expense.BaseCurrencyAmount
            : await CalculateAsync(db, expense.CompanyId, expense.Amount, expense.Currency,
                expense.OccurredAt.UtcDateTime, ct) ?? 0m;
}
