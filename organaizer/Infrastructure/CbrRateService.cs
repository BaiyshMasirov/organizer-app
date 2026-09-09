using System.Globalization;
using System.Text;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using organaizer.Domain;

namespace organaizer.Infrastructure;

public sealed record CbrRateSyncResult(DateTime Date, decimal RubToUsd, decimal RubPerUsd);

public sealed class CbrRateService(IHttpClientFactory clients, IServiceScopeFactory scopes)
{
    private const int OfficialSourceOrder = 2_000_000;

    public async Task<CbrRateSyncResult> SyncRubToUsdAsync(DateTime date, CancellationToken ct = default)
    {
        date = date.Date;
        var envelope = $"""
            <?xml version="1.0" encoding="utf-8"?>
            <soap:Envelope xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
              <soap:Body>
                <GetCursOnDate xmlns="http://web.cbr.ru/">
                  <On_date>{date.ToString("yyyy-MM-dd'T'00:00:00", CultureInfo.InvariantCulture)}</On_date>
                </GetCursOnDate>
              </soap:Body>
            </soap:Envelope>
            """;

        using var request = new HttpRequestMessage(HttpMethod.Post, "DailyInfoWebServ/DailyInfo.asmx");
        request.Headers.TryAddWithoutValidation("SOAPAction", "\"http://web.cbr.ru/GetCursOnDate\"");
        request.Content = new StringContent(envelope, Encoding.UTF8, "text/xml");
        using var response = await clients.CreateClient("cbr").SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        var xml = XDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var usd = xml.Descendants().FirstOrDefault(x =>
            x.Name.LocalName == "ValuteCursOnDate" &&
            x.Elements().FirstOrDefault(e => e.Name.LocalName == "VchCode")?.Value.Trim() == "USD");
        if (usd is null) throw new InvalidOperationException("ЦБ РФ не вернул курс USD на выбранную дату.");

        var nominal = Parse(usd, "Vnom");
        var rubPerUsd = Parse(usd, "Vcurs");
        if (nominal <= 0 || rubPerUsd <= 0) throw new InvalidOperationException("ЦБ РФ вернул некорректный курс USD.");
        var rubToUsd = nominal / rubPerUsd;

        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
        var importKey = $"cbr-rub-usd|{date:yyyy-MM-dd}";
        var entity = await db.ExchangeRates.SingleOrDefaultAsync(x => x.ImportKey == importKey, ct);
        if (entity is null)
        {
            entity = new ExchangeRate { Id = Guid.NewGuid(), Currency = "RUB", ImportKey = importKey };
            db.ExchangeRates.Add(entity);
        }
        entity.EffectiveAt = new DateTimeOffset(date, TimeSpan.Zero);
        entity.SourceOrder = OfficialSourceOrder;
        entity.RateToUsd = rubToUsd;
        entity.Note = $"ЦБ РФ · 1 USD = {rubPerUsd.ToString("0.####", CultureInfo.InvariantCulture)} RUB";
        await db.SaveChangesAsync(ct);
        return new CbrRateSyncResult(date, rubToUsd, rubPerUsd);
    }

    private static decimal Parse(XElement row, string name) => decimal.Parse(
        row.Elements().Single(x => x.Name.LocalName == name).Value,
        NumberStyles.Number,
        CultureInfo.InvariantCulture);
}

public sealed class CbrRateSyncWorker(IServiceProvider services, ILogger<CbrRateSyncWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = services.CreateScope();
                await scope.ServiceProvider.GetRequiredService<CbrRateService>()
                    .SyncRubToUsdAsync(DateTime.UtcNow.Date, stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Не удалось обновить курс RUB/USD из ЦБ РФ");
            }
            await Task.Delay(TimeSpan.FromDays(1), stoppingToken);
        }
    }
}
