using Microsoft.EntityFrameworkCore;
using organaizer.Domain;
using organaizer.Infrastructure;

namespace organaizer.Application;

public interface ICommand<TResult> { }
public interface IQuery<TResult> { }
public interface ICommandHandler<in T, TResult> where T : ICommand<TResult> { Task<TResult> Handle(T command, CancellationToken ct); }
public interface IQueryHandler<in T, TResult> where T : IQuery<TResult> { Task<TResult> Handle(T query, CancellationToken ct); }

public sealed class Dispatcher(IServiceProvider services)
{
    public Task<TResult> Send<TResult>(ICommand<TResult> command, CancellationToken ct = default) =>
        ((dynamic)services.GetRequiredService(typeof(ICommandHandler<,>).MakeGenericType(command.GetType(), typeof(TResult)))).Handle((dynamic)command, ct);
    public Task<TResult> Query<TResult>(IQuery<TResult> query, CancellationToken ct = default) =>
        ((dynamic)services.GetRequiredService(typeof(IQueryHandler<,>).MakeGenericType(query.GetType(), typeof(TResult)))).Handle((dynamic)query, ct);
}

public sealed record CreateOperationCommand(Guid CompanyId, Guid? CounterpartyId, string TypeCode,
    DateTimeOffset OccurredAt, DateTimeOffset? DueAt, string SellCurrency, decimal SellAmount,
    string BuyCurrency, decimal BuyAmount, decimal FeeAmount, string FeeCurrency,
    decimal BaseCurrencyProfit, decimal? ExchangeRate, string? SourceAccount, string? DestinationAccount, string? Note) : ICommand<Guid>;

public sealed class CreateOperationHandler(FinanceDbContext db) : ICommandHandler<CreateOperationCommand, Guid>
{
    public async Task<Guid> Handle(CreateOperationCommand c, CancellationToken ct)
    {
        if (!OperationTypes.All.ContainsKey(c.TypeCode)) throw new ArgumentException("Неизвестный тип операции");
        if (c.SellAmount <= 0 || c.BuyAmount <= 0) throw new ArgumentException("Суммы должны быть больше нуля");
        var pair = OperationTypes.Pair(c.TypeCode);
        var op = new TradeOperation { Id=Guid.NewGuid(), CompanyId=c.CompanyId, CounterpartyId=c.CounterpartyId,
            TypeCode=c.TypeCode, OccurredAt=c.OccurredAt.ToUniversalTime(), DueAt=c.DueAt?.ToUniversalTime(),
            SellCurrency=pair.Sell, SellAmount=c.SellAmount,
            BuyCurrency=pair.Buy, BuyAmount=c.BuyAmount, FeeAmount=c.FeeAmount,
            FeeCurrency=c.FeeCurrency.ToUpperInvariant(), BaseCurrencyProfit=c.BaseCurrencyProfit,
            ExchangeRate=c.ExchangeRate, SourceAccount=c.SourceAccount, DestinationAccount=c.DestinationAccount, Note=c.Note };
        db.Operations.Add(op);
        await db.SaveChangesAsync(ct);
        return op.Id;
    }
}

public sealed record UpdateOperationCommand(Guid Id, Guid CompanyId, Guid? CounterpartyId, string TypeCode,
    DateTimeOffset OccurredAt, DateTimeOffset? DueAt, string SellCurrency, decimal SellAmount,
    string BuyCurrency, decimal BuyAmount, decimal FeeAmount, string FeeCurrency,
    decimal BaseCurrencyProfit, OperationStatus Status, string? SourceAccount, string? DestinationAccount, string? Note) : ICommand<bool>;

public sealed class UpdateOperationHandler(FinanceDbContext db) : ICommandHandler<UpdateOperationCommand, bool>
{
    public async Task<bool> Handle(UpdateOperationCommand c, CancellationToken ct)
    {
        if (!OperationTypes.All.ContainsKey(c.TypeCode)) throw new ArgumentException("Неизвестный тип операции");
        if (c.SellAmount <= 0 || c.BuyAmount <= 0) throw new ArgumentException("Суммы должны быть больше нуля");
        var op = await db.Operations.SingleAsync(x => x.Id == c.Id, ct);
        op.CompanyId=c.CompanyId; op.CounterpartyId=c.CounterpartyId; op.TypeCode=c.TypeCode;
        op.OccurredAt=c.OccurredAt.ToUniversalTime(); op.DueAt=c.DueAt?.ToUniversalTime();
        op.SellCurrency=c.SellCurrency.ToUpperInvariant(); op.SellAmount=c.SellAmount;
        op.BuyCurrency=c.BuyCurrency.ToUpperInvariant(); op.BuyAmount=c.BuyAmount;
        op.FeeAmount=c.FeeAmount; op.FeeCurrency=c.FeeCurrency.ToUpperInvariant();
        op.BaseCurrencyProfit=c.BaseCurrencyProfit; op.Status=c.Status; op.SourceAccount=c.SourceAccount;
        op.DestinationAccount=c.DestinationAccount; op.Note=c.Note;
        await db.SaveChangesAsync(ct);
        return true;
    }
}

public sealed record CancelOperationCommand(Guid Id) : ICommand<bool>;
public sealed class CancelOperationHandler(FinanceDbContext db) : ICommandHandler<CancelOperationCommand, bool>
{
    public async Task<bool> Handle(CancelOperationCommand c, CancellationToken ct)
    {
        var operation=await db.Operations.SingleOrDefaultAsync(x=>x.Id==c.Id,ct);
        if(operation is null)return false;
        operation.Status=OperationStatus.Cancelled;
        await db.SaveChangesAsync(ct);
        return true;
    }
}

public sealed record CompleteOperationCommand(Guid Id) : ICommand<bool>;
public sealed class CompleteOperationHandler(FinanceDbContext db) : ICommandHandler<CompleteOperationCommand, bool>
{
    public async Task<bool> Handle(CompleteOperationCommand c, CancellationToken ct)
    {
        var operation = await db.Operations.SingleOrDefaultAsync(x => x.Id == c.Id, ct);
        if (operation is null || operation.Status == OperationStatus.Cancelled) return false;
        operation.Status = OperationStatus.Settled;
        await db.SaveChangesAsync(ct);
        return true;
    }
}

public sealed record AddSettlementCommand(Guid OperationId, Guid AccountId, DateTimeOffset OccurredAt,
    decimal Amount, string Currency, string? Note) : ICommand<bool>;

public sealed class AddSettlementHandler(FinanceDbContext db) : ICommandHandler<AddSettlementCommand, bool>
{
    public async Task<bool> Handle(AddSettlementCommand c, CancellationToken ct)
    {
        var op = await db.Operations.Include(x => x.Settlements).SingleAsync(x => x.Id == c.OperationId, ct);
        var account = await db.Accounts.SingleAsync(x => x.Id == c.AccountId, ct);
        if (account.CompanyId != op.CompanyId) throw new ArgumentException("Счет не принадлежит компании операции");
        if (!account.Currency.Equals(c.Currency, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Валюта счета не совпадает с валютой платежа");
        db.Settlements.Add(new Settlement { Id=Guid.NewGuid(), OperationId=c.OperationId, AccountId=c.AccountId,
            OccurredAt=c.OccurredAt.ToUniversalTime(), Amount=c.Amount, Currency=c.Currency.ToUpperInvariant(), Note=c.Note });
        var paidSell = op.Settlements.Where(x => x.Currency == op.SellCurrency).Sum(x => Math.Abs(x.Amount)) + (c.Currency == op.SellCurrency ? Math.Abs(c.Amount) : 0);
        var paidBuy = op.Settlements.Where(x => x.Currency == op.BuyCurrency).Sum(x => Math.Abs(x.Amount)) + (c.Currency == op.BuyCurrency ? Math.Abs(c.Amount) : 0);
        op.Status = OperationStatus.Open;
        await db.SaveChangesAsync(ct);
        return true;
    }
}

public sealed record DashboardQuery(Guid? CompanyId, DateTimeOffset From, DateTimeOffset To) : IQuery<DashboardDto>;
public sealed record BalanceDto(string Account, string Currency, decimal Balance);
public sealed record OpenOperationDto(Guid Id, DateTimeOffset Date, string Type, string Counterparty, string Debt);
public sealed record DashboardDto(decimal TradingProfit, decimal Expenses, decimal NetProfit, List<BalanceDto> Balances, List<OpenOperationDto> OpenOperations);

public sealed class DashboardHandler(FinanceDbContext db) : IQueryHandler<DashboardQuery, DashboardDto>
{
    public async Task<DashboardDto> Handle(DashboardQuery q, CancellationToken ct)
    {
        var ops = db.Operations.AsNoTracking().Where(x => x.OccurredAt >= q.From && x.OccurredAt < q.To && x.Status != OperationStatus.Cancelled);
        var expenses = db.Expenses.AsNoTracking().Where(x => x.OccurredAt >= q.From && x.OccurredAt < q.To);
        var accounts = db.Accounts.AsNoTracking().AsQueryable();
        if (q.CompanyId is { } id) { ops=ops.Where(x=>x.CompanyId==id); expenses=expenses.Where(x=>x.CompanyId==id); accounts=accounts.Where(x=>x.CompanyId==id); }
        var profit = await ops.SumAsync(x => x.BaseCurrencyProfit, ct);
        var expense = await expenses.SumAsync(x => x.BaseCurrencyAmount, ct);
        var accountRows = await accounts.Select(a => new { a.Id, a.Name, a.Currency, a.OpeningBalance }).ToListAsync(ct);
        var accountIds = accountRows.Select(x => x.Id).ToList();
        var movements = await db.Settlements.AsNoTracking().Where(x => accountIds.Contains(x.AccountId) && x.Operation!.Status != OperationStatus.Cancelled)
            .GroupBy(x => x.AccountId).Select(g => new { Id=g.Key, Amount=g.Sum(x=>x.Amount) }).ToDictionaryAsync(x=>x.Id,x=>x.Amount,ct);
        var expenseMovements = await db.Expenses.AsNoTracking().Where(x => accountIds.Contains(x.AccountId))
            .GroupBy(x => x.AccountId).Select(g => new { Id=g.Key, Amount=g.Sum(x=>x.Amount) }).ToDictionaryAsync(x=>x.Id,x=>x.Amount,ct);
        var internalMovements = await db.AccountMovements.AsNoTracking().Where(x => accountIds.Contains(x.AccountId))
            .GroupBy(x => x.AccountId).Select(g => new { Id=g.Key, Amount=g.Sum(x=>x.Amount) }).ToDictionaryAsync(x=>x.Id,x=>x.Amount,ct);
        var balances = accountRows.Select(a => new BalanceDto(a.Name,a.Currency,a.OpeningBalance + movements.GetValueOrDefault(a.Id) - expenseMovements.GetValueOrDefault(a.Id) + internalMovements.GetValueOrDefault(a.Id))).ToList();
        var open = await ops.Include(x=>x.Counterparty).Where(x=>x.Status!=OperationStatus.Settled && x.Status!=OperationStatus.Cancelled)
            .OrderByDescending(x=>x.OccurredAt).Take(12).Select(x=>new OpenOperationDto(x.Id,x.OccurredAt,x.TypeCode,x.Counterparty!=null?x.Counterparty.Name:"—",$"{x.SellAmount} {x.SellCurrency} ↔ {x.BuyAmount} {x.BuyCurrency}")).ToListAsync(ct);
        return new DashboardDto(profit,expense,profit-expense,balances,open);
    }
}

public sealed record MonthlyReportQuery(Guid? CompanyId, DateTimeOffset From, DateTimeOffset To) : IQuery<MonthlyReportDto>;
public sealed record OperationSummaryDto(string TypeCode, int Count, decimal SellAmount, string SellCurrency, decimal BuyAmount, string BuyCurrency, decimal Profit, decimal Expenses);
public sealed record CurrencyFlowDto(string Currency, decimal Incoming, decimal Outgoing, decimal Net, decimal ProfitUsd);
public sealed record BalanceSnapshotDto(string Currency,decimal Opening,decimal Closing,decimal OpeningUsdt,decimal ClosingUsdt);
public sealed record MonthlyReportDto(decimal Profit, decimal Expenses, decimal NetProfit, List<OperationSummaryDto> Operations, List<CurrencyFlowDto> Flows,List<BalanceSnapshotDto> Balances);

public sealed class MonthlyReportHandler(FinanceDbContext db) : IQueryHandler<MonthlyReportQuery, MonthlyReportDto>
{
    public async Task<MonthlyReportDto> Handle(MonthlyReportQuery q, CancellationToken ct)
    {
        var operations = db.Operations.AsNoTracking().Include(x=>x.Counterparty).Where(x => x.OccurredAt >= q.From && x.OccurredAt < q.To && x.Status != OperationStatus.Cancelled);
        var expenses = db.Expenses.AsNoTracking().Where(x => x.OccurredAt >= q.From && x.OccurredAt < q.To);
        if (q.CompanyId is { } companyId) { operations=operations.Where(x=>x.CompanyId==companyId); expenses=expenses.Where(x=>x.CompanyId==companyId); }
        var rows = await operations.ToListAsync(ct);
        var expenseRows = await expenses.ToListAsync(ct);
        var companyKinds = await db.Companies.AsNoTracking().ToDictionaryAsync(x=>x.Id,x=>x.Kind,ct);
        var rateRows=await db.ExchangeRates.AsNoTracking().Where(x=>x.EffectiveAt<q.To).OrderBy(x=>x.EffectiveAt).ThenBy(x=>x.SourceOrder).ThenBy(x=>x.ImportKey==null?1:0).ToListAsync(ct);
        var usdRates=rateRows.GroupBy(x=>x.Currency,StringComparer.OrdinalIgnoreCase).ToDictionary(g=>g.Key,g=>g.Last().RateToUsd,StringComparer.OrdinalIgnoreCase);
        usdRates["USDT"]=1m;if(!usdRates.ContainsKey("USD"))usdRates["USD"]=1m;
        decimal InUsd(decimal amount,string currency) => amount * usdRates.GetValueOrDefault(currency.ToUpperInvariant(),0m);
        decimal OperationProfit(TradeOperation x) => companyKinds.GetValueOrDefault(x.CompanyId)==CompanyKind.Broker
            ? InUsd(x.FeeAmount,x.FeeCurrency)+x.BaseCurrencyProfit
            : InUsd(x.BuyAmount,x.BuyCurrency)-InUsd(x.SellAmount,x.SellCurrency);
        var summaries = rows.GroupBy(x=>new{x.TypeCode,x.SellCurrency,x.BuyCurrency}).Select(g=>new OperationSummaryDto(g.Key.TypeCode,g.Count(),g.Sum(x=>x.SellAmount),g.Key.SellCurrency,g.Sum(x=>x.BuyAmount),g.Key.BuyCurrency,g.Sum(OperationProfit),g.Where(x=>companyKinds.GetValueOrDefault(x.CompanyId)==CompanyKind.LiquidityProvider).Sum(x=>InUsd(x.FeeAmount,x.FeeCurrency)))).OrderByDescending(x=>x.Count).ToList();
        var lpRows=rows.Where(x=>companyKinds.GetValueOrDefault(x.CompanyId)==CompanyKind.LiquidityProvider).ToList();
        var currencies = rows.SelectMany(x=>new[]{x.SellCurrency,x.BuyCurrency}).Concat(lpRows.Select(x=>x.FeeCurrency)).Concat(expenseRows.Select(x=>x.Currency)).Distinct().OrderBy(x=>x);
        var flows = currencies.Select(currency => { var incoming=rows.Where(x=>x.BuyCurrency==currency).Sum(x=>x.BuyAmount); var outgoing=rows.Where(x=>x.SellCurrency==currency).Sum(x=>x.SellAmount)+lpRows.Where(x=>x.FeeCurrency==currency).Sum(x=>x.FeeAmount)+expenseRows.Where(x=>x.Currency==currency).Sum(x=>x.Amount); var net=incoming-outgoing; return new CurrencyFlowDto(currency,incoming,outgoing,net,InUsd(net,currency)); }).ToList();
        // В Excel месячный блок расходов уже включает банковские комиссии операций.
        // Комиссии строк используем только как резерв для периодов без отдельного блока расходов.
        var selectedKinds=q.CompanyId is { } id
            ? new HashSet<CompanyKind>{companyKinds.GetValueOrDefault(id)}
            : companyKinds.Values.ToHashSet();
        var monthlyResults=(q.To-q.From).TotalDays>1?await db.MonthlyCurrencyResults.AsNoTracking().Where(x=>x.Period>=q.From&&x.Period<q.To).ToListAsync(ct):[];
        var coveredMonths=monthlyResults.Select(x=>(x.Period.Year,x.Period.Month)).ToHashSet();
        // Закрытые импортированные месяцы берем из контрольного итога Excel. Новые ручные
        // операции (ImportKey == null) и месяцы без итогового блока считаем самостоятельно.
        var liveLpRows=lpRows.Where(x=>x.ImportKey==null||!coveredMonths.Contains((x.OccurredAt.Year,x.OccurredAt.Month))).ToList();
        var expense=expenseRows.Sum(x=>InUsd(x.Amount,x.Currency))+liveLpRows.Sum(x=>InUsd(x.FeeAmount,x.FeeCurrency));
        var liquidityProfit=monthlyResults.Sum(x=>x.EquivalentUsdt)+liveLpRows.Sum(x=>InUsd(x.BuyAmount,x.BuyCurrency)-InUsd(x.SellAmount,x.SellCurrency));
        var brokerProfit=rows.Where(x=>companyKinds.GetValueOrDefault(x.CompanyId)==CompanyKind.Broker).Sum(OperationProfit);
        var profit=(selectedKinds.Contains(CompanyKind.LiquidityProvider)?liquidityProfit:0m)+(selectedKinds.Contains(CompanyKind.Broker)?brokerProfit:0m);
        if(selectedKinds.SetEquals([CompanyKind.LiquidityProvider])&&monthlyResults.Count>0)
        {
            var calculated=summaries.Sum(x=>x.Profit);
            if(calculated!=0)summaries=summaries.Select(x=>x with{Profit=x.Profit*profit/calculated}).ToList();
        }
        var balances=new List<BalanceSnapshotDto>();
        if(selectedKinds.Contains(CompanyKind.LiquidityProvider))
        {
            var snapshots=await db.MonthlyBalanceSnapshots.AsNoTracking().Where(x=>x.Period>=q.From&&x.Period<q.To).OrderByDescending(x=>x.Period).ToListAsync(ct);
            if(snapshots.Count>0){var period=snapshots[0].Period;balances=snapshots.Where(x=>x.Period==period).Select(x=>new BalanceSnapshotDto(x.Currency,x.OpeningAmount,x.ClosingAmount,x.OpeningEquivalentUsdt,x.ClosingEquivalentUsdt)).ToList();}
            else
            {
                var latestPeriod=await db.MonthlyBalanceSnapshots.AsNoTracking().Where(x=>x.Period<q.From).MaxAsync(x=>(DateTimeOffset?)x.Period,ct);
                if(latestPeriod.HasValue)
                {
                    var latest=await db.MonthlyBalanceSnapshots.AsNoTracking().Where(x=>x.Period==latestPeriod).ToListAsync(ct);var start=latestPeriod.Value.AddMonths(1);
                    var liquidityCompanyIds=companyKinds.Where(c=>c.Value==CompanyKind.LiquidityProvider).Select(c=>c.Key).ToList();
                    var priorOps=await db.Operations.AsNoTracking().Where(x=>x.OccurredAt>=start&&x.OccurredAt<q.From&&x.Status!=OperationStatus.Cancelled&&liquidityCompanyIds.Contains(x.CompanyId)).ToListAsync(ct);
                    var priorExpenses=await db.Expenses.AsNoTracking().Where(x=>x.OccurredAt>=start&&x.OccurredAt<q.From&&liquidityCompanyIds.Contains(x.CompanyId)).ToListAsync(ct);
                    foreach(var currency in latest.Select(x=>x.Currency).Concat(rows.SelectMany(x=>new[]{x.BuyCurrency,x.SellCurrency})).Distinct())
                    {
                        var last=latest.FirstOrDefault(x=>x.Currency==currency);var opening=(last?.ClosingAmount??0)+priorOps.Where(x=>x.BuyCurrency==currency).Sum(x=>x.BuyAmount)-priorOps.Where(x=>x.SellCurrency==currency).Sum(x=>x.SellAmount)-priorExpenses.Where(x=>x.Currency==currency).Sum(x=>x.Amount);
                        var closing=opening+rows.Where(x=>x.BuyCurrency==currency).Sum(x=>x.BuyAmount)-rows.Where(x=>x.SellCurrency==currency).Sum(x=>x.SellAmount)-expenseRows.Where(x=>x.Currency==currency).Sum(x=>x.Amount)-liveLpRows.Where(x=>x.FeeCurrency==currency).Sum(x=>x.FeeAmount);
                        balances.Add(new(currency,opening,closing,InUsd(opening,currency),InUsd(closing,currency)));
                    }
                }
            }
        }
        return new MonthlyReportDto(profit,expense,profit-expense,summaries,flows,balances);
    }

}
