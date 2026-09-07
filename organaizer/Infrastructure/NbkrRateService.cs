using System.Globalization;
using System.Text;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using organaizer.Domain;

namespace organaizer.Infrastructure;

public sealed class NbkrRateService(IHttpClientFactory clients, IServiceScopeFactory scopes)
{
    public static readonly string[] SupportedCurrencies = ["USD", "EUR", "CNY", "AED", "RUB"];
    private static readonly string[] Feeds = ["daily", "weekly"];

    public async Task<int> SyncAsync(CancellationToken ct = default)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var rows = new List<(string Currency, DateTime Date, decimal Nominal, decimal Value, string Feed)>();
        foreach (var feed in Feeds)
        {
            var bytes = await clients.CreateClient("nbkr").GetByteArrayAsync($"XML/{feed}.xml", ct);
            var xml = XDocument.Parse(Encoding.GetEncoding(1251).GetString(bytes));
            var date = DateTime.ParseExact(xml.Root!.Attribute("Date")!.Value, "dd.MM.yyyy", CultureInfo.InvariantCulture);
            foreach (var node in xml.Root.Elements("Currency"))
            {
                var currency = node.Attribute("ISOCode")?.Value;
                if (currency is null || !SupportedCurrencies.Contains(currency)) continue;
                rows.Add((currency, date,
                    decimal.Parse(node.Element("Nominal")!.Value.Replace(',', '.'), CultureInfo.InvariantCulture),
                    decimal.Parse(node.Element("Value")!.Value.Replace(',', '.'), CultureInfo.InvariantCulture), feed));
            }
        }
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
        var changed = 0;
        foreach (var row in rows)
        {
            var effectiveAt = new DateTimeOffset(row.Date, TimeSpan.Zero);
            var entity = await db.NbkrExchangeRates.SingleOrDefaultAsync(x => x.Currency == row.Currency && x.EffectiveAt == effectiveAt, ct);
            if (entity is null)
            {
                db.NbkrExchangeRates.Add(new NbkrExchangeRate { Id = Guid.NewGuid(), Currency = row.Currency, EffectiveAt = effectiveAt, Nominal = row.Nominal, ValueInKgs = row.Value, Feed = row.Feed });
                changed++;
            }
            else if (entity.Nominal != row.Nominal || entity.ValueInKgs != row.Value || entity.Feed != row.Feed)
            {
                entity.Nominal = row.Nominal; entity.ValueInKgs = row.Value; entity.Feed = row.Feed; entity.SyncedAt = DateTimeOffset.UtcNow;
                changed++;
            }
        }
        await db.SaveChangesAsync(ct);
        return changed;
    }

    public static async Task<decimal?> RateToUsdAsync(FinanceDbContext db, string currency, DateTime date, CancellationToken ct = default)
    {
        currency = currency.ToUpperInvariant();
        if (currency is "USD" or "USDT") return 1m;
        var end = new DateTimeOffset(date.Date.AddDays(1), TimeSpan.Zero);
        async Task<decimal?> KgsPerUnit(string code)
        {
            if (code == "KGS") return 1m;
            var row = await db.NbkrExchangeRates.AsNoTracking().Where(x => x.Currency == code && x.EffectiveAt < end)
                .OrderByDescending(x => x.EffectiveAt).FirstOrDefaultAsync(ct);
            return row is null || row.Nominal == 0 ? null : row.ValueInKgs / row.Nominal;
        }
        var currencyKgs = await KgsPerUnit(currency);
        var usdKgs = await KgsPerUnit("USD");
        return currencyKgs.HasValue && usdKgs > 0 ? currencyKgs / usdKgs : null;
    }
}

public sealed class NbkrRateSyncWorker(IServiceProvider services, ILogger<NbkrRateSyncWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = services.CreateScope();
                await scope.ServiceProvider.GetRequiredService<NbkrRateService>().SyncAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested) { logger.LogError(ex, "Не удалось обновить курсы НБКР"); }
            await Task.Delay(TimeSpan.FromHours(6), stoppingToken);
        }
    }
}
