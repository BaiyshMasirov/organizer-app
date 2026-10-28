using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using organaizer.Application;
using organaizer.Domain;
using organaizer.Infrastructure;
using organaizer.Pages.Balance;

ExcelPackage.License.SetNonCommercialOrganization("Finance Flow Tests");
var accessor=new HttpContextAccessor { HttpContext=new DefaultHttpContext { Session=new TestSession() } };
using var db=new FinanceDbContext(new DbContextOptionsBuilder<FinanceDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,accessor);
var aa=new Company {Id=Guid.NewGuid(),Name="A&A",Kind=CompanyKind.LiquidityProvider};
var orient=new Company {Id=Guid.NewGuid(),Name="Orient",Kind=CompanyKind.Broker};
var cutoff=TransferService.Instant(new DateTime(2026,9,1));
var bank=new MoneyAccount {Id=Guid.NewGuid(),CompanyId=aa.Id,Name="BAKAI",Currency="RUB",OpeningBalance=1472.59m,OpeningBalanceAt=cutoff};
var legacy=new MoneyAccount {Id=Guid.NewGuid(),CompanyId=orient.Id,Name="Legacy",Currency="RUB",OpeningBalance=300m};
db.AddRange(aa,orient,bank,legacy);
var august=cutoff.AddDays(-1);
void Settlement(DateTimeOffset at,decimal amount,bool cancelled=false){var op=new TradeOperation {Id=Guid.NewGuid(),CompanyId=aa.Id,TypeCode="TEST",SellCurrency="USD",BuyCurrency="RUB",OccurredAt=at,Status=cancelled?OperationStatus.Cancelled:OperationStatus.Settled};db.Operations.Add(op);db.Settlements.Add(new(){Id=Guid.NewGuid(),OperationId=op.Id,AccountId=bank.Id,OccurredAt=at,Amount=amount,Currency="RUB"});}
void Movement(MoneyAccount account,DateTimeOffset at,decimal amount,bool cancelled=false)=>db.AccountMovements.Add(new(){Id=Guid.NewGuid(),GroupId=Guid.NewGuid(),CompanyId=account.CompanyId,AccountId=account.Id,OccurredAt=at,Amount=amount,Currency="RUB",IsCancelled=cancelled});
Settlement(august,500m);Settlement(cutoff.AddDays(4),200m);Settlement(cutoff.AddDays(4),999m,true);
Movement(bank,august,100m);Movement(bank,cutoff,100m);Movement(bank,cutoff.AddDays(14),-20m);Movement(bank,cutoff,999m,true);Movement(legacy,august,25m);
db.Expenses.AddRange(new Expense {Id=Guid.NewGuid(),CompanyId=aa.Id,AccountId=bank.Id,OccurredAt=august,Amount=50m,Currency="RUB",Category="Old"},new Expense {Id=Guid.NewGuid(),CompanyId=aa.Id,AccountId=bank.Id,OccurredAt=cutoff.AddDays(2),Amount=10m,Currency="RUB",Category="New"});
await db.SaveChangesAsync();
var active=new ActiveCompany(accessor,db);active.Select(aa.Id);
void Check(bool value,string message){if(!value)throw new Exception(message);Console.WriteLine("PASS: "+message);}
Check(cutoff==new DateTimeOffset(2026,8,31,19,0,0,TimeSpan.Zero),"September first starts at the correct UTC boundary");
Check(await BalanceCalculator.GetAsync(db,bank.Id)==1742.59m,"Only post-opening settlements, expenses and uncancelled movements affect balance");
var september=new HistoryModel(db,active){AccountId=bank.Id,From=new DateTime(2026,9,1),To=new DateTime(2026,9,30)};
await september.OnGetAsync();
var summary=september.PeriodSummaries.Single();
Check(summary.Opening==1472.59m && summary.Income==300m && summary.Expense==30m && summary.Closing==1742.59m,"September starts at the entered balance and includes the exact start-day boundary");
Check(september.Items.First().Balance==1572.59m && september.Items.Last().Balance==1742.59m,"Running balances agree with the period summary");
var augustHistory=new HistoryModel(db,active){AccountId=bank.Id,From=new DateTime(2026,8,1),To=new DateTime(2026,8,31)};
await augustHistory.OnGetAsync();
Check(augustHistory.Items.Count==3 && augustHistory.Items.All(x=>x.Balance is null),"August transactions remain visible without fabricated balances");
Check(augustHistory.PeriodSummaries.Single().Opening is null && augustHistory.PeriodSummaries.Single().Closing is null && augustHistory.PeriodSummaries.Single().Income==0 && augustHistory.PeriodSummaries.Single().Expense==0,"August does not reuse September's initial balance or affect period turnover");
var cross=new HistoryModel(db,active){AccountId=bank.Id,From=new DateTime(2026,8,1),To=new DateTime(2026,9,30)};
await cross.OnGetAsync();
Check(cross.PeriodSummaries.Single().Opening is null && cross.PeriodSummaries.Single().Income==300m && cross.PeriodSummaries.Single().Expense==30m && cross.PeriodSummaries.Single().Closing==1742.59m,"A period spanning the opening date excludes earlier turnover and ends at the correct balance");
var october=new HistoryModel(db,active){AccountId=bank.Id,From=new DateTime(2026,10,1),To=new DateTime(2026,10,31)};
await october.OnGetAsync();
Check(october.PeriodSummaries.Single().Opening==1742.59m,"Later periods carry forward only post-opening movements");
var report=await new DashboardHandler(db).Handle(new(aa.Id,cutoff,cutoff.AddMonths(1)),default);
Check(report.Balances.Single().Balance==1742.59m,"Dashboard uses the same opening date as account balance");
var export=(FileContentResult)await september.OnGetExportAsync();
using(var package=new ExcelPackage(new MemoryStream(export.FileContents))){var sheet=package.Workbook.Worksheets[0];Check(sheet.Cells[5,3].GetValue<decimal>()==1472.59m && sheet.Cells[5,6].GetValue<decimal>()==1742.59m && sheet.Cells[5,7].GetValue<DateTime>()==new DateTime(2026,9,1),"History export includes correct balances and the opening date");}
bank.OpeningBalanceAt=TransferService.Instant(new DateTime(2026,10,1));await db.SaveChangesAsync();
Check(await BalanceCalculator.GetAsync(db,bank.Id)==1472.59m,"Editing the opening date immediately changes the balance cutoff");
active.Select(orient.Id);
Check(await BalanceCalculator.GetAsync(db,legacy.Id)==325m,"Undated accounts and Orient balances retain their original behavior");

sealed class TestSession:ISession
{
 readonly Dictionary<string,byte[]> data=[];
 public bool IsAvailable=>true;public string Id=>"test";public IEnumerable<string> Keys=>data.Keys;
 public void Clear()=>data.Clear();public void Remove(string key)=>data.Remove(key);public void Set(string key,byte[] value)=>data[key]=value;
 public bool TryGetValue(string key,out byte[] value)=>data.TryGetValue(key,out value!);
 public Task LoadAsync(CancellationToken cancellationToken=default)=>Task.CompletedTask;public Task CommitAsync(CancellationToken cancellationToken=default)=>Task.CompletedTask;
}
