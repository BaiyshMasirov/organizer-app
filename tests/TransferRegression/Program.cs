using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OfficeOpenXml;
using organaizer.Application;
using organaizer.Domain;
using organaizer.Infrastructure;

ExcelPackage.License.SetNonCommercialOrganization("Finance Flow Tests");
var accessor=new HttpContextAccessor { HttpContext=new DefaultHttpContext { Session=new TestSession() } };
using var db=new FinanceDbContext(new DbContextOptionsBuilder<FinanceDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,accessor);
var company=new Company { Id=Guid.NewGuid(),Name="Orient",Kind=CompanyKind.Broker };
var other=new Company { Id=Guid.NewGuid(),Name="Other",Kind=CompanyKind.LiquidityProvider };
MoneyAccount Account(string name,string currency,decimal opening,Guid? owner=null) => new() {
    Id=Guid.NewGuid(),CompanyId=owner??company.Id,Name=name,Currency=currency,OpeningBalance=opening };
var a=Account("BAKAI","USD",1000);var b=Account("H&H","USD",50);var c=Account("Reserve","USD",100);
var rub=Account("RUB","RUB",10000);var foreign=Account("Foreign","USD",1000,other.Id);
db.AddRange(company,other,a,b,c,rub,foreign);
db.Operations.Add(new TradeOperation { Id=Guid.NewGuid(),CompanyId=company.Id,TypeCode="OTHER_INCOME_COMMISSION",
    OccurredAt=new DateTimeOffset(2026,10,9,0,0,0,TimeSpan.Zero),SellAmount=0,BuyAmount=25,SellCurrency="USD",
    BuyCurrency="USD",BaseCurrencyProfit=25,Status=OperationStatus.Settled });
await db.SaveChangesAsync();
var active=new ActiveCompany(accessor,db);active.Select(company.Id);
void Check(bool condition,string message) { if(!condition) throw new Exception(message);Console.WriteLine("PASS: "+message); }
async Task Reject(Func<Task> action,string message) { try { await action(); } catch(ArgumentException) { Check(true,message);return; } throw new Exception(message); }
var service=new TransferService(db);var date=new DateTime(2026,10,9);
var from=new DateTimeOffset(2026,10,1,0,0,0,TimeSpan.Zero);var to=from.AddMonths(1);
var before=await new MonthlyReportHandler(db).Handle(new(company.Id,from,to),default);
await Reject(async()=>{await service.CreateAsync(company.Id,a.Id,a.Id,10,date,null,"test");},"Same-account transfer rejected");
await Reject(async()=>{await service.CreateAsync(company.Id,a.Id,rub.Id,10,date,null,"test");},"Mixed currencies rejected");
await Reject(async()=>{await service.CreateAsync(company.Id,a.Id,foreign.Id,10,date,null,"test");},"Other company account rejected");
await Reject(async()=>{await service.CreateAsync(company.Id,a.Id,b.Id,1001,date,null,"test");},"Insufficient funds rejected");
var id=await service.CreateAsync(company.Id,a.Id,b.Id,300,date,"Initial comment","alice");
Check(await BalanceCalculator.GetAsync(db,a.Id)==700 && await BalanceCalculator.GetAsync(db,b.Id)==350,"Creation moves balances once");
Check(await db.Settlements.CountAsync()==0 && await db.AccountMovements.CountAsync()==2,"Transfer uses two cash movements and no trade settlements");
var revision0=await db.TransferRevisions.SingleAsync(x=>x.TransferId==id);
Check(revision0.Amount==300 && revision0.Actor=="alice" && revision0.Action=="Создан","Original version and author preserved");
var page=new organaizer.Pages.Operations.IndexModel(db,new Dispatcher(new ServiceCollection().BuildServiceProvider()),active);
await page.OnGetAsync("BAKAI",null,TransferService.TypeCode,OperationStatuses.Completed,date,date,null);
Check(page.TotalCount==1 && page.Items.Single().Id==id,"Transfer appears in common list with account, type, status and local-date filters");
var export=(FileContentResult)await page.OnGetExportAsync(null,TransferService.TypeCode,null,date,date,null);
using(var package=new ExcelPackage(new MemoryStream(export.FileContents)))
{
    var sheet=package.Workbook.Worksheets[0];
    Check(sheet.Cells[2,3].Text.Contains(TransferService.Title) && sheet.Cells[2,6].Text=="BAKAI" && sheet.Cells[2,9].Text=="H&H","Transfer export contains both accounts and its type");
    Check(sheet.Cells[2,11].Value is null && sheet.Cells[2,10].Text=="—","Transfer export has no profit or exchange rate");
}
await service.UpdateAsync(company.Id,id,0,a.Id,c.Id,450,date.AddDays(1),new string('x',500),"bob");
Check(await BalanceCalculator.GetAsync(db,a.Id)==550 && await BalanceCalculator.GetAsync(db,b.Id)==50 && await BalanceCalculator.GetAsync(db,c.Id)==550,"Editing replaces both sides and refunds old destination");
var rows=await db.AccountMovements.Where(x=>x.GroupId==id).ToListAsync();
Check(rows.Count==2 && rows.Sum(x=>x.Amount)==0 && rows.All(x=>x.Revision==1 && x.Note!.Length==500 && x.OccurredAt.Offset==TimeSpan.Zero),"Edited pair remains balanced, UTC and at one revision");
Check(revision0.Amount==300 && revision0.Note=="Initial comment" && await db.TransferRevisions.CountAsync(x=>x.TransferId==id)==2,"Editing retains immutable original snapshot");
await Reject(()=>service.UpdateAsync(company.Id,id,1,c.Id,a.Id,200,date,null,"bob"),"Editing cannot spend the incoming side being replaced");
try { await service.UpdateAsync(company.Id,id,0,a.Id,b.Id,10,date,null,"stale");throw new Exception("Stale edit accepted"); }
catch(DbUpdateConcurrencyException) { Check(true,"Stale editor cannot overwrite a newer version"); }
await Reject(()=>service.CancelAsync(other.Id,id,1,"foreign"),"Other company cannot cancel transfer");
var after=await new MonthlyReportHandler(db).Handle(new(company.Id,from,to),default);
Check(after.Profit==before.Profit && after.Expenses==before.Expenses && after.Operations.Sum(x=>x.Count)==before.Operations.Sum(x=>x.Count)
    && after.Flows.SequenceEqual(before.Flows),"Transfer does not change income, expense, profit or trading report flows");
await service.CancelAsync(company.Id,id,1,"carol");
Check(await BalanceCalculator.GetAsync(db,a.Id)==1000 && await BalanceCalculator.GetAsync(db,b.Id)==50 && await BalanceCalculator.GetAsync(db,c.Id)==100,"Cancellation removes both effects from balances");
Check(await db.AccountMovements.CountAsync()==2 && rows.All(x=>x.IsCancelled) && await db.TransferRevisions.CountAsync()==3,"Cancellation preserves movement rows and creates an audit version");
await service.CancelAsync(company.Id,id,2,"carol");
Check(await db.TransferRevisions.CountAsync()==3,"Repeated cancellation creates no extra audit or cash effect");
await Reject(()=>service.UpdateAsync(company.Id,id,2,a.Id,b.Id,10,date,null,"bob"),"Cancelled transfer cannot be edited");
await page.OnGetAsync(null,null,TransferService.TypeCode,OperationStatuses.Cancelled,null,null,null);
Check(page.TotalCount==1,"Cancelled transfer remains visible in operations");
var history=new organaizer.Pages.Balance.HistoryModel(db,active);
await history.OnGetAsync();
Check(history.Items.All(x=>x.Type!="Перевод"),"Cancelled cash effects are excluded from balance history");
var dashboard=await new DashboardHandler(db).Handle(new(company.Id,from,to),default);
Check(dashboard.Balances.Single(x=>x.Account=="BAKAI").Balance==1000 && dashboard.NetProfit==before.NetProfit,"Dashboard balances and profit remain correct after cancellation");

sealed class TestSession : ISession
{
    private readonly Dictionary<string,byte[]> values=[];
    public bool IsAvailable=>true;public string Id=>"test";public IEnumerable<string> Keys=>values.Keys;
    public void Clear()=>values.Clear();public void Remove(string key)=>values.Remove(key);
    public void Set(string key,byte[] value)=>values[key]=value;
    public bool TryGetValue(string key,[System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out byte[]? value)=>values.TryGetValue(key,out value);
    public Task LoadAsync(CancellationToken ct=default)=>Task.CompletedTask;
    public Task CommitAsync(CancellationToken ct=default)=>Task.CompletedTask;
}
