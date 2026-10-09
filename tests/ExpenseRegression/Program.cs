using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using organaizer.Application;
using organaizer.Domain;
using organaizer.Infrastructure;
using organaizer.Pages.Expenses;

ExcelPackage.License.SetNonCommercialOrganization("Finance Flow Tests");
var accessor = new HttpContextAccessor();
using var db = new FinanceDbContext(new DbContextOptionsBuilder<FinanceDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, accessor);
var company = new Company { Id=Guid.NewGuid(), Name="Orient", Kind=CompanyKind.Broker };
var usd = new MoneyAccount { Id=Guid.NewGuid(), CompanyId=company.Id, Name="USD", Currency="USD" };
var usdReserve = new MoneyAccount { Id=Guid.NewGuid(), CompanyId=company.Id, Name="USD Reserve", Currency="USD" };
var kgs = new MoneyAccount { Id=Guid.NewGuid(), CompanyId=company.Id, Name="KGS", Currency="KGS" };
var date = new DateTime(2026,10,2);
db.AddRange(company,usd,usdReserve,kgs);
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
older.Status=OperationStatus.Cancelled;
await db.SaveChangesAsync();
var operationsExport=(FileContentResult)await operationsPage.OnGetExportAsync(null,null,null,null,null,null);
using(var operationsPackage=new ExcelPackage(new MemoryStream(operationsExport.FileContents)))
{
    var sheet=operationsPackage.Workbook.Worksheets[0];
    Check(sheet.Cells[1,1].Text=="№ п/п" && sheet.Cells[2,1].GetValue<int>()==1,"Operations export contains row numbering");
    Check(sheet.Dimension.Rows==3,"Cancelled operation is excluded from Excel export");
    Check(sheet.Cells[1,5].Text=="Валюта отдаём" && sheet.Cells[1,8].Text=="Валюта получаем" && sheet.Cells[1,12].Text=="Валюта прибыли","Operations export separates currency into filterable columns");
    Check(sheet.Cells[2,4].Value is not string && !sheet.Cells[2,4].Style.Numberformat.Format.Contains("USD"),"Operations export keeps amounts numeric without currency text");
}

var previewPage = new CreateModel(db,active);
var preview = (JsonResult)await previewPage.OnGetConvertAsync(875m,"KGS",date);
Check((decimal?)preview.Value!.GetType().GetProperty("amountUsd")!.GetValue(preview.Value)==10m,"Live KGS preview matches saved USD valuation");
var editPreview = (JsonResult)await new EditModel(db,active).OnGetConvertAsync(12.5m,"USD",date);
Check((decimal?)editPreview.Value!.GetType().GetProperty("amountUsd")!.GetValue(editPreview.Value)==12.5m,"Edit preview supports USD");
Check(await previewPage.OnGetConvertAsync(-1,"KGS",date) is BadRequestResult,"Preview rejects invalid amount");
var expenseList = new organaizer.Pages.Expenses.IndexModel(db);
await expenseList.OnGetAsync(company.Id,2026,10,null);
Check(expenseList.UsdAmounts[saved.Id]==10m,"Expense list displays legacy KGS dollar equivalent");

var copyPage = new organaizer.Pages.Operations.CreateModel(new Dispatcher(new ServiceCollection().BuildServiceProvider()),db,active);
await copyPage.OnGetAsync(older.Id);
Check(copyPage.IsCopy && copyPage.Input.OccurredAt==DateTime.Today,"Copied operation defaults to today");
Check(copyPage.Input.SellAmount==older.SellAmount && copyPage.Input.BuyAmount==older.BuyAmount,"Copy preserves both entered amounts");

var buyUsdtAedRate=OperationTypes.CanonicalRate("BUY_USDT_AED","AED",11690000m,"USDT",3181230.93m);
var sellUsdtAedRate=OperationTypes.CanonicalRate("SELL_USDT_AED","USDT",1816038.84m,"AED",6683311.67m);
Check(buyUsdtAedRate is >3.67m and <3.68m,"USDT/AED purchase rate is AED per USDT");
Check(sellUsdtAedRate is >3.67m and <3.69m,"USDT/AED sale rate keeps the same AED per USDT direction");
Check(!OperationTypes.MultiplyRate("BUY_USDT_RUB") && OperationTypes.MultiplyRate("SELL_USDT_RUB"),"Purchases divide by price and sales multiply by price");
Check(OperationTypes.Pair("BUY_AED_RUB") == ("RUB","AED") && OperationTypes.Pair("SELL_AED_RUB") == ("AED","RUB"),"Both AED/RUB conversion directions are available");
db.ExchangeRates.AddRange(
    new ExchangeRate { Id=Guid.NewGuid(), Currency="RUB", EffectiveAt=new DateTimeOffset(date,TimeSpan.Zero), SourceOrder=1, RateToUsd=.0125m },
    new ExchangeRate { Id=Guid.NewGuid(), Currency="AED", EffectiveAt=new DateTimeOffset(date,TimeSpan.Zero), SourceOrder=1, RateToUsd=.25m });
await db.SaveChangesAsync();
Check(await AaExchangeRateService.MarketRateAsync(db,"BUY_USDT_RUB",date)==80.24m && await AaExchangeRateService.MarketRateAsync(db,"SELL_USDT_RUB",date)==80.24m,"Purchase and sale load the same RUB/USDT market rate");
Check(await AaExchangeRateService.MarketRateAsync(db,"BUY_AED_RUB",date)==20m,"AED/RUB rate is loaded as RUB per AED");

var movementDate=new DateTime(2026,9,15);
var movementInstant=organaizer.Pages.Balance.IndexModel.MovementInstant(movementDate);
Check(movementInstant.Offset==TimeSpan.Zero && movementInstant.ToOffset(TimeSpan.FromHours(5)).Date==movementDate.Date && movementInstant.UtcDateTime==DateTime.SpecifyKind(movementDate.Date.AddHours(-5),DateTimeKind.Utc),"Transfer and conversion dates preserve Almaty date and are stored in UTC for PostgreSQL");
var openingGroup=Guid.NewGuid();
var periodGroup=Guid.NewGuid();
db.AccountMovements.AddRange(
    new AccountMovement { Id=Guid.NewGuid(), CompanyId=company.Id, AccountId=usdReserve.Id, GroupId=openingGroup, Kind=AccountMovementKind.Transfer, OccurredAt=organaizer.Pages.Balance.IndexModel.MovementInstant(new DateTime(2026,8,20)), Amount=-100m, Currency="USD", Note="Opening transfer" },
    new AccountMovement { Id=Guid.NewGuid(), CompanyId=company.Id, AccountId=usd.Id, GroupId=openingGroup, Kind=AccountMovementKind.Transfer, OccurredAt=organaizer.Pages.Balance.IndexModel.MovementInstant(new DateTime(2026,8,20)), Amount=100m, Currency="USD", Note="Opening transfer" },
    new AccountMovement { Id=Guid.NewGuid(), CompanyId=company.Id, AccountId=usdReserve.Id, GroupId=periodGroup, Kind=AccountMovementKind.Transfer, OccurredAt=movementInstant, Amount=-500m, Currency="USD", Note="Test transfer" },
    new AccountMovement { Id=Guid.NewGuid(), CompanyId=company.Id, AccountId=usd.Id, GroupId=periodGroup, Kind=AccountMovementKind.Transfer, OccurredAt=movementInstant, Amount=500m, Currency="USD", Note="Test transfer" });
await db.SaveChangesAsync();
var historyPage=new organaizer.Pages.Balance.HistoryModel(db,active) { AccountId=usd.Id, From=new DateTime(2026,9,1), To=new DateTime(2026,9,30) };
Check(await historyPage.OnGetAsync() is Microsoft.AspNetCore.Mvc.RazorPages.PageResult,"Filtered balance history loads");
Check(historyPage.PeriodSummaries.Single().Opening==100m && historyPage.PeriodSummaries.Single().Income==500m && historyPage.PeriodSummaries.Single().Closing==600m,"Balance history calculates opening, incoming and closing balances for the period");
Check(historyPage.Items.Count==1 && historyPage.Items[0].Description.Contains("USD Reserve"),"Internal transfer shows the source account in history");
var historyExport=(FileContentResult)await historyPage.OnGetExportAsync();
using(var historyPackage=new ExcelPackage(new MemoryStream(historyExport.FileContents)))
{
    var sheet=historyPackage.Workbook.Worksheets[0];
    var detailHeaderRow=Enumerable.Range(1,sheet.Dimension.Rows).Single(row=>sheet.Cells[row,1].Text=="№ п/п");
    Check(sheet.Cells[4,3].Text=="Сальдо на начало" && sheet.Cells[5,3].GetValue<decimal>()==100m,"Balance history export includes the period opening balance");
    Check(sheet.Cells[detailHeaderRow,6].Text=="Приход" && sheet.Cells[detailHeaderRow,7].Text=="Расход" && sheet.Cells[detailHeaderRow,8].Text=="Валюта","Balance export separates income, expense and currency");
    Check(sheet.Cells[detailHeaderRow+1,2].GetValue<DateTime>().Date==movementDate.Date,"Selected movement date is displayed in balance history export");
    Check(sheet.Cells[detailHeaderRow+1,6].GetValue<decimal>()==500m && sheet.Cells[detailHeaderRow+1,8].Text=="USD","Balance movement remains numeric with a separate currency column");
}

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
