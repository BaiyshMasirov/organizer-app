using Microsoft.EntityFrameworkCore;

namespace organaizer.Infrastructure;

/// <summary>Курсы A&amp;A: стоимость одной единицы валюты в USD.</summary>
public static class AaExchangeRateService
{
    public const decimal UsdToUsdt = 1.003m;
    public const string UsdtImportKey = "aa-usdt-usd-constant";

    public static async Task<decimal?> RateToUsdAsync(FinanceDbContext db, string currency, DateTime date, CancellationToken ct = default)
    {
        currency = currency.Trim().ToUpperInvariant();
        if (currency == "USD") return 1m;
        if (currency == "USDT")
            return await db.ExchangeRates.AsNoTracking()
                .Where(x => x.ImportKey == UsdtImportKey && x.RateToUsd > 0)
                .Select(x => (decimal?)x.RateToUsd)
                .SingleOrDefaultAsync(ct) ?? 1m / UsdToUsdt;

        var end = new DateTimeOffset(date.Date.AddDays(1), TimeSpan.Zero);
        return await db.ExchangeRates.AsNoTracking()
            .Where(x => x.Currency == currency && x.EffectiveAt < end && x.RateToUsd > 0)
            .OrderByDescending(x => x.EffectiveAt)
            .ThenByDescending(x => x.SourceOrder)
            .Select(x => (decimal?)x.RateToUsd)
            .FirstOrDefaultAsync(ct);
    }

    public static async Task<decimal?> PairRateAsync(FinanceDbContext db, string sellCurrency, string buyCurrency, DateTime date, CancellationToken ct = default)
    {
        var sell = await RateToUsdAsync(db, sellCurrency, date, ct);
        var buy = await RateToUsdAsync(db, buyCurrency, date, ct);
        return sell.HasValue && buy.HasValue && buy.Value != 0 ? sell.Value / buy.Value : null;
    }
}
