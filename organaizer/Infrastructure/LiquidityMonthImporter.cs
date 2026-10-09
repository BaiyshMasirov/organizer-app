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
        var sheet=payload.Records[0].SourceSheet;
        var expensePrefix=$"embedded-expense|liquidity|{sheet}|";
        var adjustmentPrefix=$"embedded-expense-adjustment|{sheet}|";
        var desiredExpenses=payload.Expenses.Concat(HistoricalDataImporter.ExtractEmbeddedExpenses(payload.Records)).Select(x=>x.SourceKey).ToHashSet();
        var oldExpenses=await db.Expenses.IgnoreQueryFilters().Where(x=>x.ImportKey!=null &&
            (x.ImportKey.StartsWith(expensePrefix) || x.ImportKey.StartsWith(adjustmentPrefix))).ToListAsync();
        db.Expenses.RemoveRange(oldExpenses.Where(x=>!desiredExpenses.Contains(x.ImportKey!)));
        var ratePrefix=$"summary-rate|liquidity|{sheet}|";
        var desiredRates=HistoricalDataImporter.ExtractSummaryRates(payload.Records).Select(x=>x.ImportKey).ToHashSet();
        var oldRates=await db.ExchangeRates.Where(x=>x.ImportKey!=null && x.ImportKey.StartsWith(ratePrefix)).ToListAsync();
        db.ExchangeRates.RemoveRange(oldRates.Where(x=>!desiredRates.Contains(x.ImportKey)));
        var desiredResults=HistoricalDataImporter.ExtractMonthlyResults(payload.Records).ToList();
        var period=new DateTimeOffset(periods[0].Year,periods[0].Month,1,0,0,0,TimeSpan.Zero);
        var resultCurrencies=desiredResults.Select(x=>x.Currency).ToHashSet();
        var oldResults=await db.MonthlyCurrencyResults.Where(x=>x.Period==period).ToListAsync();
        db.MonthlyCurrencyResults.RemoveRange(oldResults.Where(x=>!resultCurrencies.Contains(x.Currency)));
        await db.SaveChangesAsync();
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
