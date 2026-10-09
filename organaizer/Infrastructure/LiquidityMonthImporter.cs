using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using organaizer.Domain;

namespace organaizer.Infrastructure;

/// <summary>Imports a single prepared month and materializes its actual bank movements.</summary>
public static class LiquidityMonthImporter
{
    public static async Task ImportAsync(FinanceDbContext db, string path)
    {
        var payload = JsonSerializer.Deserialize<HistoricalDataImporter.ImportPayload>(
            await File.ReadAllTextAsync(path), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("Пустой файл импорта");
        if (payload.Operations.Count == 0 || payload.Operations.Any(x => x.CompanyKind != CompanyKind.LiquidityProvider))
            throw new InvalidOperationException("Ожидается один месяц A&A Liquidity");
        var periods = payload.Operations.Select(x => (x.OccurredAt.Year, x.OccurredAt.Month)).Distinct().ToList();
        if (periods.Count != 1 || payload.Records.Select(x => x.SourceSheet).Distinct().Count() != 1 ||
            payload.Records.Any(x => !x.SourceKey.StartsWith("liquidity|")))
            throw new InvalidOperationException("Импорт должен содержать ровно один лист и месяц");

        await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync() : null;
        await HistoricalDataImporter.ImportAsync(db, path);
        var companyId = await db.Companies.Where(x => x.Kind == CompanyKind.LiquidityProvider).Select(x => x.Id).SingleAsync();
        var keys = payload.Operations.Select(x => x.SourceKey).ToList();
        var operations = await db.Operations.IgnoreQueryFilters().Include(x => x.Settlements)
            .Where(x => x.CompanyId == companyId && keys.Contains(x.ImportKey!)).ToDictionaryAsync(x => x.ImportKey!);
        var accounts = await db.Accounts.IgnoreQueryFilters().Include(x => x.FinancialInstitution)
            .Where(x => x.CompanyId == companyId).ToListAsync();
        foreach (var item in payload.Operations)
        {
            var operation = operations[item.SourceKey];
            // Respect cancellations and manual/partial payments. Import never replaces these.
            if (operation.Status == OperationStatus.Cancelled || operation.Settlements.Count > 0) continue;
            if (operation.SellAmount != item.SellAmount || operation.BuyAmount != item.BuyAmount ||
                operation.SellCurrency != item.SellCurrency || operation.BuyCurrency != item.BuyCurrency ||
                operation.OccurredAt != item.OccurredAt)
                throw new InvalidOperationException($"Операция {item.SourceKey} изменена вручную; необходимо сверить движения перед импортом");
            operation.SourceAccount = item.SourceAccount;
            operation.DestinationAccount = item.DestinationAccount;
            Add(item.SourceAccount, item.SellCurrency, -item.SellAmount);
            Add(item.DestinationAccount, item.BuyCurrency, item.BuyAmount);

            void Add(string? name, string currency, decimal amount)
            {
                if (amount == 0) return;
                var matches = accounts.Where(x => x.Currency == currency && x.Name.Equals(name, StringComparison.OrdinalIgnoreCase)).ToList();
                if (matches.Count != 1)
                    throw new InvalidOperationException($"Неоднозначный счёт {name} ({currency}), строка {item.SourceKey}");
                db.Settlements.Add(new Settlement { Id = Guid.NewGuid(), OperationId = operation.Id,
                    AccountId = matches[0].Id, OccurredAt = operation.OccurredAt.ToUniversalTime(),
                    Currency = currency, Amount = amount, Note = $"Импорт Excel: {item.SourceKey}" });
            }
        }
        await db.SaveChangesAsync();
        if (transaction is not null) await transaction.CommitAsync();
    }
}
