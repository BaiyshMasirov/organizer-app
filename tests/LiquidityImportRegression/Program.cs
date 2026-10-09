using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using organaizer.Application;
using organaizer.Domain;
using organaizer.Infrastructure;

var path = args.Length > 0 ? args[0] : "imports/august-2026.json";
using var db = new FinanceDbContext(new DbContextOptionsBuilder<FinanceDbContext>()
    .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, new HttpContextAccessor());
var company = new Company { Id=Guid.NewGuid(), Name="A&A Liquidity", Kind=CompanyKind.LiquidityProvider };
db.Companies.Add(company);
await db.SaveChangesAsync();
void Check(bool ok, string message) { if(!ok) throw new Exception(message); Console.WriteLine("PASS: " + message); }
await LiquidityMonthImporter.ImportAsync(db, path);
Check(await db.Operations.CountAsync()==385, "All 385 August rows, including one-sided and credit movements, import");
Check(await db.Settlements.CountAsync()==755, "755 actual cash movements import; zero sides create no payment");
var credit = await db.Operations.SingleAsync(x=>x.ImportKey=="liquidity|Август 2026|328");
Check(credit.OccurredAt.Date==new DateTime(2026,8,25), "Missing credit date inherits previous row");
Check(await db.Settlements.Where(x=>x.OperationId==credit.Id).SumAsync(x=>x.Amount)==-750750m, "Credit withdrawal is recorded on Ledger");
var baka = await db.Settlements.Include(x=>x.Account).SingleAsync(x=>x.Operation!.ImportKey=="liquidity|Август 2026|3" && x.Amount>0);
Check(baka.Account!.Name=="BAKAI" && baka.Account.Currency=="USD" && baka.Amount==28100m, "Receiving bank uses receiving currency");
var from = new DateTimeOffset(2026,8,1,0,0,0,TimeSpan.Zero);
var to = from.AddMonths(1);
var report = await new MonthlyReportHandler(db).Handle(new(company.Id,from,to),default);
Check(Math.Abs(report.Expenses-151460.7926m)<0.01m, "Expenses match Excel without duplicate commissions");
Check(Math.Abs(report.NetProfit-412133.1297m)<0.01m, "Net profit matches Excel");
foreach(var balance in report.Balances)
{
    var flow=report.Flows.Single(x=>x.Currency==balance.Currency);
    Check(Math.Abs(balance.Opening+flow.Net-balance.Closing)<0.01m, $"{balance.Currency} opening + net cash flow reconciles to Excel closing");
}
var dashboard=await new DashboardHandler(db).Handle(new(company.Id,from,to),default);
Check(dashboard.NetProfit==report.NetProfit && dashboard.Expenses==report.Expenses, "Dashboard and monthly report agree");
var ids = await db.MonthlyBalanceSnapshots.ToDictionaryAsync(x=>x.ImportKey,x=>x.Id);
var resultCount = await db.MonthlyCurrencyResults.CountAsync();
var snapshot = await db.MonthlyBalanceSnapshots.SingleAsync(x=>x.Currency=="USD");
var expectedClosing = snapshot.ClosingAmount;
snapshot.ClosingAmount = -1m;
await db.SaveChangesAsync();
var balances = new Dictionary<Guid,decimal>();
foreach(var account in await db.Accounts.ToListAsync()) balances[account.Id]=await BalanceCalculator.GetAsync(db,account.Id);
await LiquidityMonthImporter.ImportAsync(db,path);
Check(await db.Operations.CountAsync()==385 && await db.Settlements.CountAsync()==755, "Repeat import creates no duplicate operations or payments");
Check(await db.MonthlyBalanceSnapshots.CountAsync()==4 && await db.MonthlyCurrencyResults.CountAsync()==resultCount, "Repeat import creates no duplicate monthly summaries");
Check((await db.MonthlyBalanceSnapshots.ToListAsync()).All(x=>ids[x.ImportKey]==x.Id), "Monthly summaries retain their identities");
Check(snapshot.ClosingAmount==expectedClosing, "Repeat import refreshes stale monthly balances from Excel");
foreach(var account in await db.Accounts.ToListAsync())
    Check(balances[account.Id]==await BalanceCalculator.GetAsync(db,account.Id), $"Repeat import preserves balance of {account.Name} {account.Currency}");

// Exercise an upgrade from the old extractor: omitted zero sides and reversed bank labels.
using var legacyDb = new FinanceDbContext(new DbContextOptionsBuilder<FinanceDbContext>()
    .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, new HttpContextAccessor());
legacyDb.Companies.Add(new Company { Id=Guid.NewGuid(), Name="A&A Liquidity", Kind=CompanyKind.LiquidityProvider });
await legacyDb.SaveChangesAsync();
var payload = System.Text.Json.JsonSerializer.Deserialize<HistoricalDataImporter.ImportPayload>(
    await File.ReadAllTextAsync(path), new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive=true })!;
var legacy = payload with { Records=payload.Records.Select(x=>x.SourceRow>=394
        ? x with { SourceRow=x.SourceRow+1000, SourceKey=$"liquidity|{x.SourceSheet}|{x.SourceRow+1000}" } : x).ToList(),
    Operations=payload.Operations.Where(x=>x.SellAmount>0 && x.BuyAmount>0)
    .Select(x=>x with { SourceAccount=x.DestinationAccount, DestinationAccount=x.SourceAccount }).ToList() };
var legacyPath = Path.GetTempFileName();
try
{
    await File.WriteAllTextAsync(legacyPath,System.Text.Json.JsonSerializer.Serialize(legacy));
    await HistoricalDataImporter.ImportAsync(legacyDb,legacyPath);
    var oldOperation=await legacyDb.Operations.SingleAsync(x=>x.ImportKey=="liquidity|Август 2026|3");
    var oldId=oldOperation.Id;
    oldOperation.SellAmount=398207.17m;
    oldOperation.BuyAmount=399600.1m;
    oldOperation.Note=null;
    var oldRaw=await legacyDb.HistoricalImportRecords.SingleAsync(x=>x.SourceKey==oldOperation.ImportKey);
    var oldJson=System.Text.Json.Nodes.JsonNode.Parse(oldRaw.DataJson)!;
    oldJson["cells"]![9]=oldOperation.SellAmount;
    oldJson["cells"]![4]=oldOperation.BuyAmount;
    oldRaw.DataJson=oldJson.ToJsonString();
    await legacyDb.SaveChangesAsync();
    await LiquidityMonthImporter.ImportAsync(legacyDb,path);
    Check(await legacyDb.Operations.CountAsync()==385 && await legacyDb.Settlements.CountAsync()==755,
        "Upgrading legacy August import fills missing rows and bank payments without duplicates");
    var repaired = await legacyDb.Operations.SingleAsync(x=>x.ImportKey=="liquidity|Август 2026|3");
    Check(repaired.Id==oldId && repaired.SellAmount==27959.5m && repaired.BuyAmount==28100m,
        "Updated source refreshes untouched original imports while preserving operation identity");
    Check(repaired.SourceAccount=="Vexel" && repaired.DestinationAccount=="BAKAI",
        "Legacy reversed bank labels are repaired for operations without manual payments");
    var upgradedReport=await new MonthlyReportHandler(legacyDb).Handle(new(await legacyDb.Companies.Select(x=>x.Id).SingleAsync(),from,to),default);
    Check(Math.Abs(upgradedReport.NetProfit-412133.1297m)<0.01m && Math.Abs(upgradedReport.Expenses-151460.7926m)<0.01m
        && await legacyDb.MonthlyBalanceSnapshots.CountAsync()==4,
        "Shifted Excel summary and expense rows reconcile without duplicate monthly figures");
}
finally { File.Delete(legacyPath); }
