using System.Globalization;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using organaizer.Domain;
using organaizer.Infrastructure;

public static class ExecutiveDashboard
{
    private sealed record CompanyResult(string Company, decimal Profit, decimal Expenses, decimal Net);
    private sealed record PairTurnover(string Pair, int Count, decimal SellAmount, string SellCurrency, decimal BuyAmount, string BuyCurrency);
    private sealed record OrientTurnover(string Direction, string Pair, int Count, decimal UsdtAmount, decimal CounterAmount, string CounterCurrency);
    private sealed record AccountBalance(string Company, string Account, string Kind, string Currency, decimal Balance);

    public static void MapExecutiveDashboard(this WebApplication app) => app.MapGet("/executive", HandleAsync);

    private static async Task<IResult> HandleAsync(FinanceDbContext db, HttpContext context, DateTime? from, DateTime? to)
    {
        if (!await HasAccessAsync(db, context.User.FindFirstValue(ClaimTypes.NameIdentifier))) return Results.Forbid();

        var today = DateTime.UtcNow.Date;
        var dateFrom = (from ?? new DateTime(today.Year, 1, 1)).Date;
        var dateTo = (to ?? today).Date;
        if (dateTo < dateFrom) (dateFrom, dateTo) = (dateTo, dateFrom);
        var start = new DateTimeOffset(dateFrom, TimeSpan.Zero);
        var end = new DateTimeOffset(dateTo.AddDays(1), TimeSpan.Zero);
        var ru = CultureInfo.GetCultureInfo("ru-RU");
        string E(object? value) => HtmlEncoder.Default.Encode(value?.ToString() ?? "");
        string N(decimal value) => value.ToString("N2", ru);

        var companies = await db.Companies.IgnoreQueryFilters().AsNoTracking().OrderBy(x => x.Kind).ToListAsync();
        var operations = await db.Operations.IgnoreQueryFilters().AsNoTracking().Where(x => x.Status != OperationStatus.Cancelled && x.OccurredAt >= start && x.OccurredAt < end).ToListAsync();
        var expenses = await db.Expenses.IgnoreQueryFilters().AsNoTracking().Where(x => x.OccurredAt >= start && x.OccurredAt < end).ToListAsync();
        var results = companies.Select(company =>
        {
            var profit = operations.Where(x => x.CompanyId == company.Id).Sum(x => x.BaseCurrencyProfit);
            var expense = expenses.Where(x => x.CompanyId == company.Id).Sum(x => x.BaseCurrencyAmount);
            return new CompanyResult(company.Name, profit, expense, profit - expense);
        }).ToList();

        var aa = companies.FirstOrDefault(x => x.Kind == CompanyKind.LiquidityProvider);
        var aaTurnovers = aa is null ? [] : operations.Where(x => x.CompanyId == aa.Id).GroupBy(x => new { x.SellCurrency, x.BuyCurrency })
            .Select(g => new PairTurnover($"{g.Key.SellCurrency}/{g.Key.BuyCurrency}", g.Count(), g.Sum(x => x.SellAmount), g.Key.SellCurrency, g.Sum(x => x.BuyAmount), g.Key.BuyCurrency)).OrderByDescending(x => x.Count).ToList();
        var orient = companies.FirstOrDefault(x => x.Kind == CompanyKind.Broker);
        var orientTurnovers = orient is null ? [] : operations.Where(x => x.CompanyId == orient.Id && (x.SellCurrency == "USDT" || x.BuyCurrency == "USDT"))
            .GroupBy(x => new { IsBuy = x.BuyCurrency == "USDT", Counter = x.BuyCurrency == "USDT" ? x.SellCurrency : x.BuyCurrency })
            .Select(g => new OrientTurnover(g.Key.IsBuy ? "Покупка USDT" : "Продажа USDT", g.Key.IsBuy ? $"{g.Key.Counter}/USDT" : $"USDT/{g.Key.Counter}", g.Count(), g.Sum(x => g.Key.IsBuy ? x.BuyAmount : x.SellAmount), g.Sum(x => g.Key.IsBuy ? x.SellAmount : x.BuyAmount), g.Key.Counter)).OrderBy(x => x.Direction).ThenBy(x => x.Pair).ToList();

        var accounts = await db.Accounts.IgnoreQueryFilters().AsNoTracking().Include(x => x.Company).Include(x => x.FinancialInstitution)
            .Where(x => x.IsActive && x.FinancialInstitution != null && (x.FinancialInstitution.Kind == InstitutionKind.Bank || x.FinancialInstitution.Kind == InstitutionKind.Wallet)).ToListAsync();
        var ids = accounts.Select(x => x.Id).ToList();
        var settlements = await db.Settlements.IgnoreQueryFilters().AsNoTracking().Where(x => ids.Contains(x.AccountId) && x.Operation!.Status != OperationStatus.Cancelled).GroupBy(x => x.AccountId).Select(g => new { Id = g.Key, Amount = g.Sum(x => x.Amount) }).ToDictionaryAsync(x => x.Id, x => x.Amount);
        var accountExpenses = await db.Expenses.IgnoreQueryFilters().AsNoTracking().Where(x => ids.Contains(x.AccountId)).GroupBy(x => x.AccountId).Select(g => new { Id = g.Key, Amount = g.Sum(x => x.Amount) }).ToDictionaryAsync(x => x.Id, x => x.Amount);
        var movements = await db.AccountMovements.IgnoreQueryFilters().AsNoTracking().Where(x => ids.Contains(x.AccountId)).GroupBy(x => x.AccountId).Select(g => new { Id = g.Key, Amount = g.Sum(x => x.Amount) }).ToDictionaryAsync(x => x.Id, x => x.Amount);
        var balances = accounts.Select(x => new AccountBalance(x.Company?.Name ?? "—", x.FinancialInstitution?.Name ?? x.Name, x.FinancialInstitution?.Kind == InstitutionKind.Bank ? "Банк" : "Кошелёк", x.Currency, x.OpeningBalance + settlements.GetValueOrDefault(x.Id) - accountExpenses.GetValueOrDefault(x.Id) + movements.GetValueOrDefault(x.Id))).OrderBy(x => x.Company).ThenBy(x => x.Account).ThenBy(x => x.Currency).ToList();

        var companyRows = new StringBuilder();
        foreach (var x in results) companyRows.Append($"<tr><td><b>{E(x.Company)}</b></td><td>{N(x.Profit)} USD</td><td class=neg>{N(x.Expenses)} USD</td><td class='{(x.Net < 0 ? "neg" : "pos")}'>{N(x.Net)} USD</td></tr>");
        var aaRows = new StringBuilder();
        foreach (var x in aaTurnovers) aaRows.Append($"<tr><td><b>{E(x.Pair)}</b></td><td>{x.Count}</td><td>{N(x.SellAmount)} {E(x.SellCurrency)}</td><td>{N(x.BuyAmount)} {E(x.BuyCurrency)}</td></tr>");
        if (aaTurnovers.Count == 0) aaRows.Append("<tr><td colspan=4 class=empty>Нет операций за период</td></tr>");
        var orientRows = new StringBuilder();
        foreach (var x in orientTurnovers) orientRows.Append($"<tr><td><b>{E(x.Direction)}</b></td><td>{E(x.Pair)}</td><td>{x.Count}</td><td>{N(x.UsdtAmount)} USDT</td><td>{N(x.CounterAmount)} {E(x.CounterCurrency)}</td></tr>");
        if (orientTurnovers.Count == 0) orientRows.Append("<tr><td colspan=5 class=empty>Нет операций за период</td></tr>");
        var balanceRows = new StringBuilder();
        foreach (var x in balances) balanceRows.Append($"<tr><td>{E(x.Company)}</td><td><b>{E(x.Account)}</b></td><td>{E(x.Kind)}</td><td>{E(x.Currency)}</td><td class='{(x.Balance < 0 ? "neg" : "")}'>{N(x.Balance)} {E(x.Currency)}</td></tr>");

        var companyLabels = JsonSerializer.Serialize(results.Select(x => x.Company));
        var companyValues = JsonSerializer.Serialize(results.Select(x => x.Net));
        var orientLabels = JsonSerializer.Serialize(orientTurnovers.Select(x => $"{x.Direction} {x.Pair}"));
        var orientValues = JsonSerializer.Serialize(orientTurnovers.Select(x => x.UsdtAmount));
        var html = """<!doctype html><html lang=ru><head><meta charset=utf-8><meta name=viewport content="width=device-width"><title>Дашборд руководителя · Finance Analytics</title><script src="https://cdn.jsdelivr.net/npm/chart.js@4.4.7/dist/chart.umd.min.js"></script><style>:root{--nav:#111d3f;--primary:#4f46e5;--bg:#f4f6fa;--line:#e5e9f2}*{box-sizing:border-box}body{margin:0;background:var(--bg);color:#17213a;font:14px Arial}.shell{display:grid;grid-template-columns:245px 1fr;min-height:100vh}aside{background:var(--nav);color:#fff;padding:26px 20px}.brand{font-size:22px;font-weight:800;margin-bottom:35px}.nav-title{margin:0 12px 10px;color:#92a0c4;font-size:11px;font-weight:700;text-transform:uppercase}.nav{display:block;padding:13px 12px;border-radius:9px;color:#dce3f7;text-decoration:none;margin:7px 0}.nav:hover{background:#ffffff12}.nav.active{background:var(--primary);color:#fff;font-weight:700}.content{padding:28px;max-width:1500px;width:100%}header{display:flex;justify-content:space-between;align-items:center;margin-bottom:22px}h1{margin:0 0 5px}.muted{color:#748097}.period{background:#e8e7ff;color:#4338ca;padding:8px 13px;border-radius:20px;font-weight:700}.filter,.card,.metric{background:#fff;border:1px solid var(--line);border-radius:13px;box-shadow:0 3px 14px #17213a08}.filter{padding:17px;display:grid;grid-template-columns:1fr 1fr 1fr;gap:12px;margin-bottom:18px}label{display:block;font-size:12px;font-weight:700;margin-bottom:6px}input,button{width:100%;height:39px;border:1px solid #d7dce8;border-radius:8px;padding:0 10px;background:#fff}button{background:var(--primary);color:#fff;border:0;font-weight:700;margin-top:18px}.metrics{display:grid;grid-template-columns:repeat(3,1fr);gap:14px;margin-bottom:18px}.metric{padding:19px}.metric span{display:block;color:#748097}.metric strong{display:block;font-size:24px;margin-top:7px}.charts{display:grid;grid-template-columns:repeat(2,1fr);gap:16px;margin-bottom:18px}.card{margin-bottom:18px;overflow:hidden}.card h2{font-size:16px;margin:0;padding:17px;border-bottom:1px solid var(--line)}.chart{height:310px;padding:16px}.scroll{overflow:auto}table{width:100%;border-collapse:collapse}th,td{padding:12px 14px;border-bottom:1px solid var(--line);text-align:right;white-space:nowrap}th:first-child,td:first-child{text-align:left}th{font-size:12px;color:#667085;background:#fafbfc}.pos{color:#16875b;font-weight:700}.neg{color:#dc3545;font-weight:700}.empty{text-align:center!important;color:#748097;padding:30px}@media(max-width:900px){.shell{grid-template-columns:1fr}aside{padding:18px}.brand{margin-bottom:18px}.content{padding:16px}.filter,.metrics,.charts{grid-template-columns:1fr}}</style></head><body><div class=shell><aside><div class=brand>Finance Analytics</div><div class=nav-title>Разделы</div><a class=nav href=/>Диаграммы и итоги</a><a class="nav active" href=/executive>Для руководителя</a></aside><main class=content><header><div><h1>Дашборд руководителя</h1><div class=muted>Ключевые показатели по двум компаниям</div></div><span class=period>__FROM__ — __TO__</span></header><form class=filter><div><label>Дата от</label><input type=date name=from value=__FROM_VALUE__></div><div><label>Дата до</label><input type=date name=to value=__TO_VALUE__></div><div><button>Показать</button></div></form><div class=metrics><div class=metric><span>Прибыль</span><strong>__PROFIT__ USD</strong></div><div class=metric><span>Расходы</span><strong class=neg>__EXPENSES__ USD</strong></div><div class=metric><span>Финансовый результат</span><strong class=__NET_CLASS__>__NET__ USD</strong></div></div><div class=charts><div class=card><h2>Финансовый результат по компаниям</h2><div class=chart><canvas id=companyChart></canvas></div></div><div class=card><h2>Покупка и продажа USDT · Orient Capital</h2><div class=chart><canvas id=orientChart></canvas></div></div></div><div class=card><h2>Финансовый результат</h2><div class=scroll><table><thead><tr><th>Компания</th><th>Прибыль</th><th>Расходы</th><th>Результат</th></tr></thead><tbody>__COMPANY_ROWS__</tbody></table></div></div><div class=card><h2>Обороты по валютным парам · A&amp;A Liquidity</h2><div class=scroll><table><thead><tr><th>Пара</th><th>Операций</th><th>Отдали</th><th>Получили</th></tr></thead><tbody>__AA_ROWS__</tbody></table></div></div><div class=card><h2>Обороты покупки/продажи USDT · Orient Capital</h2><div class=scroll><table><thead><tr><th>Направление</th><th>Пара</th><th>Операций</th><th>Оборот USDT</th><th>Вторая сторона</th></tr></thead><tbody>__ORIENT_ROWS__</tbody></table></div></div><div class=card><h2>Остатки по банковским счетам и кошелькам</h2><div class=scroll><table><thead><tr><th>Компания</th><th>Счёт / кошелёк</th><th>Тип</th><th>Валюта</th><th>Остаток</th></tr></thead><tbody>__BALANCE_ROWS__</tbody></table></div></div></main></div><script>const companyLabels=__COMPANY_LABELS__,companyValues=__COMPANY_VALUES__,orientLabels=__ORIENT_LABELS__,orientValues=__ORIENT_VALUES__;new Chart(companyChart,{type:'bar',data:{labels:companyLabels,datasets:[{label:'Результат, USD',data:companyValues,backgroundColor:companyValues.map(x=>x<0?'#ef4444':'#4f46e5'),borderRadius:7}]},options:{maintainAspectRatio:false}});new Chart(orientChart,{type:'bar',data:{labels:orientLabels,datasets:[{label:'Оборот, USDT',data:orientValues,backgroundColor:'#10b981',borderRadius:7}]},options:{maintainAspectRatio:false,indexAxis:'y'}});</script></body></html>""";
        html = html.Replace("__FROM__", dateFrom.ToString("dd.MM.yyyy")).Replace("__TO__", dateTo.ToString("dd.MM.yyyy")).Replace("__FROM_VALUE__", dateFrom.ToString("yyyy-MM-dd")).Replace("__TO_VALUE__", dateTo.ToString("yyyy-MM-dd"))
            .Replace("__PROFIT__", N(results.Sum(x => x.Profit))).Replace("__EXPENSES__", N(results.Sum(x => x.Expenses))).Replace("__NET__", N(results.Sum(x => x.Net))).Replace("__NET_CLASS__", results.Sum(x => x.Net) < 0 ? "neg" : "pos")
            .Replace("__COMPANY_ROWS__", companyRows.ToString()).Replace("__AA_ROWS__", aaRows.ToString()).Replace("__ORIENT_ROWS__", orientRows.ToString()).Replace("__BALANCE_ROWS__", balanceRows.ToString())
            .Replace("__COMPANY_LABELS__", companyLabels).Replace("__COMPANY_VALUES__", companyValues).Replace("__ORIENT_LABELS__", orientLabels).Replace("__ORIENT_VALUES__", orientValues);
        return Results.Content(html, "text/html");
    }

    private static async Task<bool> HasAccessAsync(FinanceDbContext db, string? userId)
    {
        if (string.IsNullOrWhiteSpace(userId)) return false;
        var super = await (from ur in db.UserRoles.AsNoTracking() join role in db.Roles.AsNoTracking() on ur.RoleId equals role.Id where ur.UserId == userId && role.Name == AppPermissions.SuperAdminRole select ur).AnyAsync();
        return super || await db.UserClaims.AsNoTracking().AnyAsync(x => x.UserId == userId && x.ClaimType == AppPermissions.ClaimType && x.ClaimValue == AppPermissions.Executive.View);
    }
}
