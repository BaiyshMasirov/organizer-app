using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using organaizer.Domain;

namespace organaizer.Infrastructure;

public static class HistoricalDataImporter
{
    public static async Task ImportAsync(FinanceDbContext db, string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;
        await using var stream=File.OpenRead(path);
        var payload=await JsonSerializer.DeserializeAsync<ImportPayload>(stream,new JsonSerializerOptions{PropertyNameCaseInsensitive=true});
        if(payload is null)return;
        var companies=await db.Companies.ToDictionaryAsync(x=>x.Kind);
        var clientCache=(await db.Counterparties.ToListAsync()).GroupBy(x=>x.CompanyId).ToDictionary(g=>g.Key,g=>g.ToDictionary(x=>x.Name.Trim().ToLower(),StringComparer.OrdinalIgnoreCase));
        var rawKeys=(await db.HistoricalImportRecords.Select(x=>x.SourceKey).ToListAsync()).ToHashSet();
        foreach(var r in payload.Records.Where(x=>rawKeys.Add(x.SourceKey))) db.HistoricalImportRecords.Add(new HistoricalImportRecord{Id=Guid.NewGuid(),SourceKey=r.SourceKey,SourceFile=r.SourceFile,SourceSheet=r.SourceSheet,SourceRow=r.SourceRow,RecordType=r.RecordType,DataJson=r.DataJson});
        await db.SaveChangesAsync();
        var operationKeys=(await db.Operations.Where(x=>x.ImportKey!=null).Select(x=>x.ImportKey!).ToListAsync()).ToHashSet();
        foreach(var item in payload.Operations.Where(x=>operationKeys.Add(x.SourceKey)))
        {
            var company=companies[item.CompanyKind];var client=FindOrCreateClient(db,clientCache,company.Id,item.Counterparty);
            db.Operations.Add(new TradeOperation{Id=Guid.NewGuid(),CompanyId=company.Id,CounterpartyId=client?.Id,TypeCode=item.TypeCode,OccurredAt=item.OccurredAt,DueAt=null,SellCurrency=item.SellCurrency,SellAmount=item.SellAmount,BuyCurrency=item.BuyCurrency,BuyAmount=item.BuyAmount,FeeAmount=item.FeeAmount,FeeCurrency=item.FeeCurrency,BaseCurrencyProfit=item.BaseCurrencyProfit,Status=OperationStatus.Settled,Note=item.Note,ExchangeRate=item.ExchangeRate,SourceAccount=item.SourceAccount,DestinationAccount=item.DestinationAccount,ImportKey=item.SourceKey});
        }
        await db.SaveChangesAsync();
        await EnsureOperationAccounts(db,companies,payload.Operations);
        var importedRates=await db.ExchangeRates.Where(x=>x.ImportKey!=null).ToDictionaryAsync(x=>x.ImportKey!);var rateKeys=importedRates.Keys.ToHashSet();
        foreach(var item in payload.Operations.OrderBy(x=>x.OccurredAt))
        {
            var sellBase=item.SellCurrency=="USDT";var buyBase=item.BuyCurrency=="USDT";
            if(sellBase==buyBase)continue;
            var currency=sellBase?item.BuyCurrency:item.SellCurrency;var amount=sellBase?item.BuyAmount:item.SellAmount;var usd=sellBase?item.SellAmount:item.BuyAmount;
            var key=$"rate|{item.SourceKey}|{currency}";if(amount<=0)continue;
            if(importedRates.TryGetValue(key,out var existingRate)){existingRate.SourceOrder=SourceRow(item.SourceKey);continue;}
            if(!rateKeys.Add(key))continue;
            db.ExchangeRates.Add(new ExchangeRate{Id=Guid.NewGuid(),Currency=currency,EffectiveAt=item.OccurredAt,SourceOrder=SourceRow(item.SourceKey),RateToUsd=usd/amount,Note="Импортировано из последней операции Excel",ImportKey=key});
        }
        foreach(var rate in ExtractSummaryRates(payload.Records))
        {
            if(importedRates.TryGetValue(rate.ImportKey!,out var existing)){existing.RateToUsd=rate.RateToUsd;existing.SourceOrder=rate.SourceOrder;continue;}
            if(rateKeys.Add(rate.ImportKey!))db.ExchangeRates.Add(rate);
        }
        await db.SaveChangesAsync();
        var resultKeys=(await db.MonthlyCurrencyResults.Select(x=>x.ImportKey).ToListAsync()).ToHashSet();
        foreach(var result in ExtractMonthlyResults(payload.Records).Where(x=>resultKeys.Add(x.ImportKey)))db.MonthlyCurrencyResults.Add(result);
        await db.SaveChangesAsync();
        await ImportMonthlyTables(db,payload.Records);
        var importedExpenses=await db.Expenses.Where(x=>x.ImportKey!=null).ToDictionaryAsync(x=>x.ImportKey!);var expenseKeys=importedExpenses.Keys.ToHashSet();
        var accountCache=(await db.Accounts.ToListAsync()).ToDictionary(x=>$"{x.CompanyId}|{x.Currency}|{x.Name}",StringComparer.OrdinalIgnoreCase);
        var allExpenses=payload.Expenses.Concat(ExtractEmbeddedExpenses(payload.Records)).ToList();
        foreach(var item in allExpenses)
        {
            var company=companies[item.CompanyKind];var accountName="Импорт: "+item.Account;var accountKey=$"{company.Id}|{item.Currency}|{accountName}";
            if(!accountCache.TryGetValue(accountKey,out var account)){account=new MoneyAccount{Id=Guid.NewGuid(),CompanyId=company.Id,Name=accountName,Kind=AccountKind.Bank,Currency=item.Currency};accountCache[accountKey]=account;db.Accounts.Add(account);}
            if(importedExpenses.TryGetValue(item.SourceKey,out var existing)){existing.CompanyId=company.Id;existing.AccountId=account.Id;existing.OccurredAt=item.OccurredAt;existing.Category=item.Category;existing.Amount=item.Amount;existing.Currency=item.Currency;existing.BaseCurrencyAmount=item.BaseCurrencyAmount;existing.Note=item.Note;continue;}
            if(expenseKeys.Add(item.SourceKey))db.Expenses.Add(new Expense{Id=Guid.NewGuid(),CompanyId=company.Id,AccountId=account.Id,OccurredAt=item.OccurredAt,Category=item.Category,Amount=item.Amount,Currency=item.Currency,BaseCurrencyAmount=item.BaseCurrencyAmount,Note=item.Note,ImportKey=item.SourceKey});
        }
        await db.SaveChangesAsync();
    }

    private static int SourceRow(string key)=>int.TryParse(key.Split('|').LastOrDefault(),out var row)?row:0;

    private static async Task EnsureOperationAccounts(FinanceDbContext db,Dictionary<CompanyKind,Company> companies,IEnumerable<OperationRecord> operations)
    {
        static string? Clean(string? value)
        {
            var text=value?.Trim();
            if(string.IsNullOrWhiteSpace(text)||text is "-" or "0"||decimal.TryParse(text,out _))return null;
            return text.Length>160?text[..160]:text;
        }
        static InstitutionKind InstitutionType(string name)=>name.Contains("Vexel",StringComparison.OrdinalIgnoreCase)?InstitutionKind.PaymentSystem:name.Contains("Ledger",StringComparison.OrdinalIgnoreCase)?InstitutionKind.Wallet:name.Contains("BINANCE",StringComparison.OrdinalIgnoreCase)||name.Contains("KRAKEN",StringComparison.OrdinalIgnoreCase)||name.Contains("Coinex",StringComparison.OrdinalIgnoreCase)?InstitutionKind.Exchange:InstitutionKind.Bank;
        static AccountKind AccountType(InstitutionKind kind)=>kind switch{InstitutionKind.Exchange=>AccountKind.Exchange,InstitutionKind.Wallet or InstitutionKind.PaymentSystem=>AccountKind.CryptoWallet,_=>AccountKind.Bank};

        var institutions=(await db.FinancialInstitutions.ToListAsync()).ToDictionary(x=>x.Name,StringComparer.OrdinalIgnoreCase);
        var accounts=(await db.Accounts.IgnoreQueryFilters().Where(x=>x.FinancialInstitutionId!=null).ToListAsync()).GroupBy(x=>$"{x.CompanyId}|{x.FinancialInstitutionId}|{x.Currency}",StringComparer.OrdinalIgnoreCase).ToDictionary(x=>x.Key,x=>x.First(),StringComparer.OrdinalIgnoreCase);
        foreach(var operation in operations)
        {
            foreach(var side in new[]{(Name:Clean(operation.SourceAccount),Currency:operation.SellCurrency),(Name:Clean(operation.DestinationAccount),Currency:operation.BuyCurrency)})
            {
                if(side.Name is null)continue;
                if(!institutions.TryGetValue(side.Name,out var institution))
                {
                    institution=new FinancialInstitution{Id=Guid.NewGuid(),Name=side.Name,Kind=InstitutionType(side.Name),Note="Добавлено из столбцов банка исходного Excel"};
                    institutions[side.Name]=institution;db.FinancialInstitutions.Add(institution);
                }
                var companyId=companies[operation.CompanyKind].Id;var key=$"{companyId}|{institution.Id}|{side.Currency}";
                if(accounts.ContainsKey(key))continue;
                var account=new MoneyAccount{Id=Guid.NewGuid(),CompanyId=companyId,FinancialInstitutionId=institution.Id,Name=side.Name,Kind=AccountType(institution.Kind),Currency=side.Currency,OpeningBalance=0};
                accounts[key]=account;db.Accounts.Add(account);
            }
        }
        await db.SaveChangesAsync();
    }

    private static IEnumerable<ExpenseRecord> ExtractEmbeddedExpenses(IEnumerable<RawRecord> records)
    {
        var months=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase){{"январь",1},{"февраль",2},{"март",3},{"апрель",4},{"май",5},{"июнь",6},{"июль",7},{"август",8},{"сентябрь",9},{"октябрь",10},{"ноябрь",11},{"декабрь",12}};
        foreach(var sheet in records.Where(x=>x.SourceKey.StartsWith("liquidity|",StringComparison.OrdinalIgnoreCase)).GroupBy(x=>x.SourceSheet))
        {
            var rows=sheet.OrderBy(x=>x.SourceRow).Select(x=>(Record:x,Cells:JsonSerializer.Deserialize<CellBag>(x.DataJson)?.Cells??[])).ToList();
            var title=rows.FirstOrDefault(x=>x.Cells.Count>0&&Text(x.Cells[0]).StartsWith("расходы ",StringComparison.OrdinalIgnoreCase));
            if(title.Record is null)continue;
            var words=Text(title.Cells[0]).Split(' ',StringSplitOptions.RemoveEmptyEntries);if(words.Length<3||!months.TryGetValue(words[1],out var month)||!int.TryParse(words[2],out var year))continue;
            var summaryHeader=rows.FirstOrDefault(x=>x.Cells.Count>9&&string.Equals(Text(x.Cells[8]),"расходы",StringComparison.OrdinalIgnoreCase));
            var summary=rows.Where(x=>summaryHeader.Record is not null&&x.Record.SourceRow>summaryHeader.Record.SourceRow&&x.Record.SourceRow<=summaryHeader.Record.SourceRow+4&&x.Cells.Count>9&&Number(x.Cells[8],out _)).ToDictionary(x=>Text(x.Cells[7]).ToUpperInvariant(),x=>{Number(x.Cells[8],out var amount);return amount;});
            var detailRows=rows.Where(x=>x.Record.SourceRow>title.Record.SourceRow&&x.Cells.Count>=4&&Number(x.Cells[2],out _)&&
                (string.Equals(Text(x.Cells[1]),words[1],StringComparison.OrdinalIgnoreCase)||x.Cells[1].ValueKind==JsonValueKind.Number)).ToList();
            var assigned=new Dictionary<string,decimal>(StringComparer.OrdinalIgnoreCase);
            foreach(var row in detailRows)
            {
                Number(row.Cells[2],out var amount);var description=string.Join(' ',row.Cells.Skip(3).Take(2).Select(Text)).ToUpperInvariant();
                var currency=description.Contains("AED")?"AED":description.Contains("RUB")||description.Contains("РУБ")||description.Contains("ВТБ")||description.Contains("ОТП")||description.Contains("МТС")||description.Contains("АЛЬФА")||description.Contains("СОВКОМ")||description.Contains("УНИФОНД")||description.Contains("МБАНК")?"RUB":description.Contains("USD")||description.Contains("H&H")||description.Contains("ХХ")?"USD":"USDT";
                assigned[currency]=assigned.GetValueOrDefault(currency)+amount;var date=new DateTimeOffset(year,month,DateTime.DaysInMonth(year,month),0,0,0,TimeSpan.Zero);
                yield return new ExpenseRecord($"embedded-expense|{row.Record.SourceKey}",CompanyKind.LiquidityProvider,date,string.IsNullOrWhiteSpace(description)?"Расход":Text(row.Cells[3]),amount,currency,currency is "USD" or "USDT"?amount:0,"Прочие расходы",$"Импорт из {row.Record.SourceSheet}, строка {row.Record.SourceRow}");
            }
            foreach(var item in summary)
            {
                var adjustment=item.Value-assigned.GetValueOrDefault(item.Key);if(Math.Abs(adjustment)<0.00000001m)continue;var date=new DateTimeOffset(year,month,DateTime.DaysInMonth(year,month),0,0,0,TimeSpan.Zero);
                yield return new ExpenseRecord($"embedded-expense-adjustment|{sheet.Key}|{item.Key}",CompanyKind.LiquidityProvider,date,"Сверка с итогом Excel",adjustment,item.Key,item.Key is "USD" or "USDT"?adjustment:0,"Прочие расходы","Автоматическая сверка валютной суммы с итоговым блоком Excel");
            }
        }
    }

    private static IEnumerable<ExchangeRate> ExtractSummaryRates(IEnumerable<RawRecord> records)
    {
        foreach(var sheet in records.Where(x=>x.SourceKey.StartsWith("liquidity|",StringComparison.OrdinalIgnoreCase)).GroupBy(x=>x.SourceSheet))
        {
            var rows=sheet.OrderBy(x=>x.SourceRow).Select(x=>(Record:x,Cells:JsonSerializer.Deserialize<CellBag>(x.DataJson)?.Cells??[])).ToList();
            var header=rows.FirstOrDefault(x=>x.Cells.Count>9&&string.Equals(Text(x.Cells[8]),"расходы",StringComparison.OrdinalIgnoreCase));
            if(header.Record is null)continue;
            foreach(var row in rows.Where(x=>x.Record.SourceRow>header.Record.SourceRow&&x.Record.SourceRow<=header.Record.SourceRow+4))
            {
                if(row.Cells.Count<10)continue;var currency=Text(row.Cells[7]).ToUpperInvariant();
                if(currency.Length is <2 or >5||!Number(row.Cells[8],out var amount)||amount<=0||!Number(row.Cells[9],out var equivalent))continue;
                var monthDate=rows.Where(x=>x.Record.SourceRow<header.Record.SourceRow&&x.Cells.Count>1&&x.Cells[1].ValueKind==JsonValueKind.Number).Select(x=>x.Cells[1].GetDouble()).LastOrDefault();
                if(monthDate<=0)continue;var date=DateTimeOffset.FromUnixTimeMilliseconds((long)(new DateTime(1899,12,30,0,0,0,DateTimeKind.Utc).AddDays(monthDate)-DateTime.UnixEpoch).TotalMilliseconds);
                yield return new ExchangeRate{Id=Guid.NewGuid(),Currency=currency,EffectiveAt=new DateTimeOffset(date.Year,date.Month,DateTime.DaysInMonth(date.Year,date.Month),0,0,0,TimeSpan.Zero),SourceOrder=100000+row.Record.SourceRow,RateToUsd=equivalent/amount,Note=$"Итоговый курс Excel ({row.Record.SourceSheet})",ImportKey=$"summary-rate|{row.Record.SourceKey}|{currency}"};
            }
        }
    }

    private static IEnumerable<MonthlyCurrencyResult> ExtractMonthlyResults(IEnumerable<RawRecord> records)
    {
        foreach(var sheet in records.Where(x=>x.SourceKey.StartsWith("liquidity|",StringComparison.OrdinalIgnoreCase)).GroupBy(x=>x.SourceSheet))
        {
            var rows=sheet.OrderBy(x=>x.SourceRow).Select(x=>(Record:x,Cells:JsonSerializer.Deserialize<CellBag>(x.DataJson)?.Cells??[])).ToList();
            var header=rows.FirstOrDefault(x=>x.Cells.Count>8&&string.Equals(Text(x.Cells[3]),"получили",StringComparison.OrdinalIgnoreCase)&&string.Equals(Text(x.Cells[8]),"разница",StringComparison.OrdinalIgnoreCase));
            if(header.Record is null)continue;
            var serial=rows.Where(x=>x.Record.SourceRow<header.Record.SourceRow&&x.Cells.Count>1&&x.Cells[1].ValueKind==JsonValueKind.Number).Select(x=>x.Cells[1].GetDouble()).LastOrDefault();if(serial<=0)continue;
            var date=new DateTime(1899,12,30,0,0,0,DateTimeKind.Utc).AddDays(serial);var period=new DateTimeOffset(date.Year,date.Month,1,0,0,0,TimeSpan.Zero);
            var extracted=new List<MonthlyCurrencyResult>();
            foreach(var row in rows.Where(x=>x.Record.SourceRow>header.Record.SourceRow&&x.Record.SourceRow<=header.Record.SourceRow+4))
            {
                if(row.Cells.Count<10)continue;var currency=Text(row.Cells[7]).ToUpperInvariant();if(currency.Length is <2 or >5||!Number(row.Cells[8],out var net)||!Number(row.Cells[9],out var equivalent))continue;
                extracted.Add(new MonthlyCurrencyResult{Id=Guid.NewGuid(),Period=period,Currency=currency,NetAmount=net,EquivalentUsdt=equivalent,ImportKey=$"monthly-result|{row.Record.SourceKey}|{currency}"});
            }
            foreach(var result in extracted)yield return result;
            var total=rows.FirstOrDefault(x=>x.Cells.Count>11&&Text(x.Cells[8]).Contains("ИТОГО РАСХОДЫ",StringComparison.OrdinalIgnoreCase));
            if(total.Record is not null&&Number(total.Cells[9],out var expenses)&&Number(total.Cells[11],out var netProfit))
            {
                var other=netProfit+expenses-extracted.Sum(x=>x.EquivalentUsdt);
                if(Math.Abs(other)>0.005m)yield return new MonthlyCurrencyResult{Id=Guid.NewGuid(),Period=period,Currency="OTHER",NetAmount=other,EquivalentUsdt=other,ImportKey=$"monthly-result|{sheet.Key}|OTHER"};
            }
        }
    }

    private static async Task ImportMonthlyTables(FinanceDbContext db,IEnumerable<RawRecord> records)
    {
        var purchaseKeys=(await db.MonthlyPurchaseTotals.Select(x=>x.ImportKey).ToListAsync()).ToHashSet();var saleKeys=(await db.MonthlySaleTotals.Select(x=>x.ImportKey).ToListAsync()).ToHashSet();var expenseKeys=(await db.MonthlyExpenseTotals.Select(x=>x.ImportKey).ToListAsync()).ToHashSet();var balanceKeys=(await db.MonthlyBalanceSnapshots.Select(x=>x.ImportKey).ToListAsync()).ToHashSet();
        foreach(var sheet in records.Where(x=>x.SourceKey.StartsWith("liquidity|",StringComparison.OrdinalIgnoreCase)).GroupBy(x=>x.SourceSheet))
        {
            var rows=sheet.OrderBy(x=>x.SourceRow).Select(x=>(Record:x,Cells:JsonSerializer.Deserialize<CellBag>(x.DataJson)?.Cells??[])).ToList();var period=PeriodFromRows(rows);if(period is null)continue;var expenseHeader=rows.FirstOrDefault(x=>x.Cells.Count>9&&string.Equals(Text(x.Cells[8]),"расходы",StringComparison.OrdinalIgnoreCase)).Record?.SourceRow??-100;var balanceHeader=rows.FirstOrDefault(x=>x.Cells.Count>9&&Text(x.Cells[8]).Contains("сальдо на начало",StringComparison.OrdinalIgnoreCase)).Record?.SourceRow??-100;
            foreach(var row in rows)
            {
                if(row.Cells.Count>4){var label=Text(row.Cells[1]);var isBuy=label.StartsWith("ИТОГО покупка ",StringComparison.OrdinalIgnoreCase);var isSale=label.StartsWith("ИТОГО продажа ",StringComparison.OrdinalIgnoreCase);if((isBuy||isSale)&&Number(row.Cells[3],out var received)&&Number(row.Cells[4],out var given)){var pair=label.Split(' ').Last();var parts=pair.Split('/');if(parts.Length==2){var receivedCurrency=isBuy?parts[0]:parts[1];var givenCurrency=isBuy?parts[1]:parts[0];var key=$"monthly-trade|{row.Record.SourceKey}";if(isBuy&&purchaseKeys.Add(key))db.MonthlyPurchaseTotals.Add(new(){Id=Guid.NewGuid(),Period=period.Value,Pair=pair,ReceivedAmount=received,ReceivedCurrency=receivedCurrency,GivenAmount=given,GivenCurrency=givenCurrency,ImportKey=key});if(isSale&&saleKeys.Add(key))db.MonthlySaleTotals.Add(new(){Id=Guid.NewGuid(),Period=period.Value,Pair=pair,ReceivedAmount=received,ReceivedCurrency=receivedCurrency,GivenAmount=given,GivenCurrency=givenCurrency,ImportKey=key});}}}
                if(row.Record.SourceRow>expenseHeader&&row.Record.SourceRow<=expenseHeader+4&&row.Cells.Count>9&&row.Cells[7].ValueKind==JsonValueKind.String&&Number(row.Cells[8],out var expense)&&Number(row.Cells[9],out var expenseUsdt)){var currency=Text(row.Cells[7]).ToUpperInvariant();if(currency is "USD" or "USDT" or "RUB" or "AED"){var key=$"monthly-expense-total|{row.Record.SourceKey}";if(expenseKeys.Add(key))db.MonthlyExpenseTotals.Add(new(){Id=Guid.NewGuid(),Period=period.Value,Currency=currency,Amount=expense,EquivalentUsdt=expenseUsdt,ImportKey=key});}}
                if(row.Record.SourceRow>balanceHeader&&row.Record.SourceRow<=balanceHeader+4&&row.Cells.Count>11&&row.Cells[7].ValueKind==JsonValueKind.String&&Number(row.Cells[8],out var opening)&&Number(row.Cells[9],out var closing)&&Number(row.Cells[10],out var openingUsdt)&&Number(row.Cells[11],out var closingUsdt)){var currency=Text(row.Cells[7]).ToUpperInvariant();if(currency is "USD" or "USDT" or "RUB" or "AED"){var key=$"monthly-balance|{row.Record.SourceKey}";if(balanceKeys.Add(key))db.MonthlyBalanceSnapshots.Add(new(){Id=Guid.NewGuid(),Period=period.Value,Currency=currency,OpeningAmount=opening,ClosingAmount=closing,OpeningEquivalentUsdt=openingUsdt,ClosingEquivalentUsdt=closingUsdt,ImportKey=key});}}
            }
        }
        await db.SaveChangesAsync();
    }

    private static DateTimeOffset? PeriodFromRows(List<(RawRecord Record,List<JsonElement> Cells)> rows)
    {
        var serial=rows.Where(x=>x.Cells.Count>1&&x.Cells[1].ValueKind==JsonValueKind.Number).Select(x=>x.Cells[1].GetDouble()).LastOrDefault();if(serial<=0)return null;var date=new DateTime(1899,12,30,0,0,0,DateTimeKind.Utc).AddDays(serial);return new DateTimeOffset(date.Year,date.Month,1,0,0,0,TimeSpan.Zero);
    }

    private static string Text(JsonElement value)=>value.ValueKind==JsonValueKind.String?(value.GetString()??"").Trim():value.ToString().Trim();
    private static bool Number(JsonElement value,out decimal result)=>value.ValueKind==JsonValueKind.Number?value.TryGetDecimal(out result):decimal.TryParse(Text(value),System.Globalization.NumberStyles.Any,System.Globalization.CultureInfo.InvariantCulture,out result);
    private sealed record CellBag([property:System.Text.Json.Serialization.JsonPropertyName("cells")]List<JsonElement> Cells);

    private static Counterparty? FindOrCreateClient(FinanceDbContext db,Dictionary<Guid,Dictionary<string,Counterparty>> cache,Guid companyId,string? name)
    {
        if(string.IsNullOrWhiteSpace(name))return null;var clean=name.Trim();if(clean.Length>180)clean=clean[..180];var normalized=clean.ToLower();if(!cache.TryGetValue(companyId,out var companyClients)){companyClients=new(StringComparer.OrdinalIgnoreCase);cache[companyId]=companyClients;}if(companyClients.TryGetValue(normalized,out var existing))return existing;var client=new Counterparty{Id=Guid.NewGuid(),CompanyId=companyId,Name=clean,Kind=CounterpartyKind.Client};companyClients[normalized]=client;db.Counterparties.Add(client);return client;
    }

    public sealed record ImportPayload(List<RawRecord> Records,List<OperationRecord> Operations,List<ExpenseRecord> Expenses);
    public sealed record RawRecord(string SourceKey,string SourceFile,string SourceSheet,int SourceRow,string RecordType,string DataJson);
    public sealed record OperationRecord(string SourceKey,CompanyKind CompanyKind,string TypeCode,DateTimeOffset OccurredAt,string? Counterparty,string SellCurrency,decimal SellAmount,string BuyCurrency,decimal BuyAmount,decimal FeeAmount,string FeeCurrency,decimal BaseCurrencyProfit,decimal? ExchangeRate,string? SourceAccount,string? DestinationAccount,string? Note);
    public sealed record ExpenseRecord(string SourceKey,CompanyKind CompanyKind,DateTimeOffset OccurredAt,string Category,decimal Amount,string Currency,decimal BaseCurrencyAmount,string Account,string? Note);
}
