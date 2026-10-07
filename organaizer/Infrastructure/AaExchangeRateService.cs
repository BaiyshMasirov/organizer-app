using Microsoft.EntityFrameworkCore;

namespace organaizer.Infrastructure;

/// <summary>Курсы A&amp;A: стоимость одной единицы валюты в USD.</summary>
public static class AaExchangeRateService
{
    public const decimal UsdtToUsd = 1.003m;
    public const string UsdtImportKey = "aa-usdt-usd-constant";

    public static async Task<decimal?> RateToUsdAsync(FinanceDbContext db, string currency, DateTime date, CancellationToken ct = default)
    {
        currency = currency.Trim().ToUpperInvariant();
        if (currency == "USD") return 1m;
        if (currency == "USDT")
            return await db.ExchangeRates.AsNoTracking()
                .Where(x => x.ImportKey == UsdtImportKey && x.RateToUsd > 0)
                .Select(x => (decimal?)x.RateToUsd)
                .SingleOrDefaultAsync(ct) ?? UsdtToUsd;

        var end = new DateTimeOffset(date.Date.AddDays(1), TimeSpan.Zero);
        return await db.ExchangeRates.AsNoTracking()
            .Where(x => x.Currency == currency && x.EffectiveAt < end && x.RateToUsd > 0)
            .OrderByDescending(x => x.EffectiveAt)
            .ThenByDescending(x => x.SourceOrder)
            .Select(x => (decimal?)x.RateToUsd)
            .FirstOrDefaultAsync(ct);
    }

    /// <summary>Рыночный курс в едином формате «деньги / товар», независимо от направления операции.</summary>
    public static async Task<decimal?> MarketRateAsync(FinanceDbContext db, string typeCode, DateTime date, CancellationToken ct = default)
    {
        var pair = Domain.OperationTypes.MarketPair(typeCode);
        var baseRate = await RateToUsdAsync(db, pair.Base, date, ct);
        var quoteRate = await RateToUsdAsync(db, pair.Quote, date, ct);
        return baseRate.HasValue && quoteRate.HasValue && quoteRate.Value != 0
            ? baseRate.Value / quoteRate.Value
            : null;
    }
}
