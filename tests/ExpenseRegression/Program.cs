using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using organaizer.Application;
using organaizer.Domain;
using organaizer.Infrastructure;
using organaizer.Pages.Expenses;

var accessor = new HttpContextAccessor();
using var db = new FinanceDbContext(new DbContextOptionsBuilder<FinanceDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, accessor);
var company = new Company { Id=Guid.NewGuid(), Name="Orient", Kind=CompanyKind.Broker };
var usd = new MoneyAccount { Id=Guid.NewGuid(), CompanyId=company.Id, Name="USD", Currency="USD" };
var kgs = new MoneyAccount { Id=Guid.NewGuid(), CompanyId=company.Id, Name="KGS", Currency="KGS" };
var date = new DateTime(2026,10,2);
db.AddRange(company,usd,kgs);
db.NbkrExchangeRates.Add(new() { Id=Guid.NewGuid(), Currency="USD", EffectiveAt=new DateTimeOffset(date,TimeSpan.Zero), Feed="test", Nominal=1, ValueInKgs=87.5m });
db.NbkrExchangeRates.Add(new() { Id=Guid.NewGuid(), Currency="USD", EffectiveAt=new DateTimeOffset(date.AddDays(1),TimeSpan.Zero), Feed="test", Nominal=1, ValueInKgs=100m });
await db.SaveChangesAsync();
var active = new ActiveCompany(accessor,db);
ExpenseForm Form(MoneyAccount account, decimal amount) => new() { CompanyId=company.Id, AccountId=account.Id, Currency=account.Currency, Amount=amount, Category="Test", OccurredAt=date };
void Check(bool ok,string message) { if(!ok) throw new Exception(message); Console.WriteLine("PASS: "+message); }
var create = new CreateModel(db,active) { Input=Form(usd,12.5m) };
Check(await create.OnPostAsync() is RedirectToPageResult,"USD expense saves");
var saved = await db.Expenses.SingleAsync();
Check(saved.BaseCurrencyAmount==12.5m,"USD equivalent equals amount");
var edit = new EditModel(db,active) { Input=Form(kgs,875m) };
edit.Input.Id=saved.Id;
Check(await edit.OnPostAsync() is RedirectToPageResult,"Expense edit saves");
Check(saved.BaseCurrencyAmount==10m,"KGS conversion uses expense date, excluding future rate");
saved.BaseCurrencyAmount=0; // Legacy record written by the old form.
await db.SaveChangesAsync();
var from=new DateTimeOffset(date,TimeSpan.Zero); var to=from.AddDays(1);
var report=await new MonthlyReportHandler(db).Handle(new(company.Id,from,to),default);
Check(report.Expenses==10m && report.NetProfit==-10m,"Report includes legacy KGS expense using NBKR");
var dashboard=await new DashboardHandler(db).Handle(new(company.Id,from,to),default);
Check(dashboard.Expenses==10m,"Dashboard includes legacy KGS expense");
Check(await ExpenseValuation.CalculateAsync(db,company.Id,100,"XYZ",date) is null,"Missing rate is not valued as zero");


accessor.HttpContext = new DefaultHttpContext { Session = new TestSession() };
active.Select(company.Id);
var pageContext = new Microsoft.AspNetCore.Mvc.RazorPages.PageContext {
    ViewData = new Microsoft.AspNetCore.Mvc.ViewFeatures.ViewDataDictionary(
        new Microsoft.AspNetCore.Mvc.ModelBinding.EmptyModelMetadataProvider(),
        new Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary())
};
var formPage = new CreateModel(db,active) { PageContext=pageContext };
await formPage.OnGetAsync();
Check(formPage.Input.CompanyId==company.Id && formPage.Companies.Count()==1,"Form selects the active company");
formPage.Input=Form(kgs,10);
formPage.Input.Currency="USD";
Check(await formPage.OnPostAsync() is Microsoft.AspNetCore.Mvc.RazorPages.PageResult && formPage.ModelState.ContainsKey("Input.Currency"),"Currency mismatch returns a field error");
Check(await db.Expenses.CountAsync()==1,"Invalid expense does not save");

var altyn = new Counterparty { Id=Guid.NewGuid(), CompanyId=company.Id, Name="Алтын брокер" };
var altSpot = new Counterparty { Id=Guid.NewGuid(), CompanyId=company.Id, Name="Альт Спот" };
db.AddRange(altyn,altSpot);
TradeOperation Operation(Guid id, Counterparty client, DateTimeOffset created) => new() {
    Id=id, CompanyId=company.Id, CounterpartyId=client.Id, TypeCode="test",
    OccurredAt=from, CreatedAt=created, SellCurrency="USD", BuyCurrency="RUB",
    SellAmount=100, BuyAmount=8750, Note="Алтын — комментарий к чужому клиенту"
};
var older=Operation(Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"),altyn,from.AddHours(1));
var newer=Operation(Guid.Parse("00000000-0000-0000-0000-000000000001"),altyn,from.AddHours(2));
var unrelated=Operation(Guid.NewGuid(),altSpot,from.AddHours(3));
db.AddRange(older,newer,unrelated);
await db.SaveChangesAsync();
var operationsPage=new organaizer.Pages.Operations.IndexModel(db,new Dispatcher(new Microsoft.Extensions.DependencyInjection.ServiceCollection().BuildServiceProvider()),active);
await operationsPage.OnGetAsync(" АЛТЫН ",null,null,null,null,null,null);
Check(operationsPage.TotalCount==2 && operationsPage.Items.All(x=>x.CounterpartyId==altyn.Id),"Client search matches full text regardless of case and excludes hidden notes");
Check(operationsPage.Items.Select(x=>x.Id).SequenceEqual(new[]{newer.Id,older.Id}),"Newest creation comes first within the same operation date, regardless of GUID");
await operationsPage.OnGetAsync("алтын",null,null,null,null,null,"dateAsc");
Check(operationsPage.Items.Select(x=>x.Id).SequenceEqual(new[]{older.Id,newer.Id}),"Ascending order preserves creation chronology");
await operationsPage.OnGetAsync("RUB",null,null,null,null,null,null);
Check(operationsPage.TotalCount==3,"Currency search remains available");

sealed class TestSession : ISession
{
    readonly Dictionary<string,byte[]> values=new();
    public bool IsAvailable=>true;
    public string Id=>"test";
    public IEnumerable<string> Keys=>values.Keys;
    public void Clear()=>values.Clear();
    public Task CommitAsync(CancellationToken cancellationToken=default)=>Task.CompletedTask;
    public Task LoadAsync(CancellationToken cancellationToken=default)=>Task.CompletedTask;
    public void Remove(string key)=>values.Remove(key);
    public void Set(string key,byte[] value)=>values[key]=value;
    public bool TryGetValue(string key,out byte[] value)=>values.TryGetValue(key,out value!);
}
