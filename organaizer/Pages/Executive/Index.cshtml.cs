using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using organaizer.Domain;
using organaizer.Infrastructure;

namespace organaizer.Pages.Executive;

public sealed class IndexModel(FinanceDbContext db) : PageModel
{
    public sealed record CompanyResult(string Company, decimal Profit, decimal Expenses, decimal Net);
    public sealed record PairTurnover(string Pair, int Count, decimal SellAmount, string SellCurrency, decimal BuyAmount, string BuyCurrency);
    public sealed record OrientTurnover(string Direction, string Pair, int Count, decimal UsdtAmount, decimal CounterAmount, string CounterCurrency);
    public sealed record AccountBalance(string Company, string Account, string Kind, string Currency, decimal Balance);

    public DateTime From { get; private set; }
    public DateTime To { get; private set; }
    public decimal TotalProfit { get; private set; }
    public decimal TotalExpenses { get; private set; }
    public decimal TotalNet { get; private set; }
    public List<CompanyResult> CompanyResults { get; private set; } = [];
    public List<PairTurnover> AaTurnovers { get; private set; } = [];
    public List<OrientTurnover> OrientTurnovers { get; private set; } = [];
    public List<AccountBalance> Balances { get; private set; } = [];
    public string CompanyLabels { get; private set; } = "[]";
    public string CompanyValues { get; private set; } = "[]";
    public string OrientLabels { get; private set; } = "[]";
    public string OrientValues { get; private set; } = "[]";

    public async Task OnGetAsync(DateTime? from, DateTime? to)
    {
        var today = DateTime.UtcNow.Date;
        From = (from ?? new DateTime(today.Year, 1, 1)).Date;
        To = (to ?? today).Date;
        if (To < From) (From, To) = (To, From);
        var start = new DateTimeOffset(From, TimeSpan.Zero);
        var end = new DateTimeOffset(To.AddDays(1), TimeSpan.Zero);

        var companies = await db.Companies.IgnoreQueryFilters().AsNoTracking().OrderBy(x => x.Kind).ToListAsync();
        var operations = await db.Operations.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.Status != OperationStatus.Cancelled && x.OccurredAt >= start && x.OccurredAt < end).ToListAsync();
        var expenses = await db.Expenses.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.OccurredAt >= start && x.OccurredAt < end).ToListAsync();

        CompanyResults = companies.Select(company =>
        {
            var profit = operations.Where(x => x.CompanyId == company.Id).Sum(x => x.BaseCurrencyProfit);
            var expense = expenses.Where(x => x.CompanyId == company.Id).Sum(x => x.BaseCurrencyAmount);
            return new CompanyResult(company.Name, profit, expense, profit - expense);
        }).ToList();
        TotalProfit = CompanyResults.Sum(x => x.Profit);
        TotalExpenses = CompanyResults.Sum(x => x.Expenses);
        TotalNet = CompanyResults.Sum(x => x.Net);

        var aa = companies.FirstOrDefault(x => x.Kind == CompanyKind.LiquidityProvider);
        if (aa is not null)
            AaTurnovers = operations.Where(x => x.CompanyId == aa.Id).GroupBy(x => new { x.SellCurrency, x.BuyCurrency })
                .Select(g => new PairTurnover($"{g.Key.SellCurrency}/{g.Key.BuyCurrency}", g.Count(), g.Sum(x => x.SellAmount), g.Key.SellCurrency, g.Sum(x => x.BuyAmount), g.Key.BuyCurrency))
                .OrderByDescending(x => x.Count).ToList();

        var orient = companies.FirstOrDefault(x => x.Kind == CompanyKind.Broker);
        if (orient is not null)
            OrientTurnovers = operations.Where(x => x.CompanyId == orient.Id && (x.SellCurrency == "USDT" || x.BuyCurrency == "USDT"))
                .GroupBy(x => new { IsBuy = x.BuyCurrency == "USDT", Counter = x.BuyCurrency == "USDT" ? x.SellCurrency : x.BuyCurrency })
                .Select(g => new OrientTurnover(g.Key.IsBuy ? "Покупка USDT" : "Продажа USDT", g.Key.IsBuy ? $"{g.Key.Counter}/USDT" : $"USDT/{g.Key.Counter}", g.Count(), g.Sum(x => g.Key.IsBuy ? x.BuyAmount : x.SellAmount), g.Sum(x => g.Key.IsBuy ? x.SellAmount : x.BuyAmount), g.Key.Counter))
                .OrderBy(x => x.Direction).ThenBy(x => x.Pair).ToList();

        var accounts = await db.Accounts.IgnoreQueryFilters().AsNoTracking().Include(x => x.Company).Include(x => x.FinancialInstitution)
            .Where(x => x.IsActive && x.FinancialInstitution != null && (x.FinancialInstitution.Kind == InstitutionKind.Bank || x.FinancialInstitution.Kind == InstitutionKind.Wallet)).ToListAsync();
        var ids = accounts.Select(x => x.Id).ToList();
        var settlements = await db.Settlements.IgnoreQueryFilters().AsNoTracking().Where(x => ids.Contains(x.AccountId) && x.Operation!.Status != OperationStatus.Cancelled).GroupBy(x => x.AccountId).Select(g => new { Id = g.Key, Amount = g.Sum(x => x.Amount) }).ToDictionaryAsync(x => x.Id, x => x.Amount);
        var accountExpenses = await db.Expenses.IgnoreQueryFilters().AsNoTracking().Where(x => ids.Contains(x.AccountId)).GroupBy(x => x.AccountId).Select(g => new { Id = g.Key, Amount = g.Sum(x => x.Amount) }).ToDictionaryAsync(x => x.Id, x => x.Amount);
        var movements = await db.AccountMovements.IgnoreQueryFilters().AsNoTracking().Where(x => ids.Contains(x.AccountId)).GroupBy(x => x.AccountId).Select(g => new { Id = g.Key, Amount = g.Sum(x => x.Amount) }).ToDictionaryAsync(x => x.Id, x => x.Amount);
        Balances = accounts.Select(x => new AccountBalance(x.Company?.Name ?? "—", x.FinancialInstitution?.Name ?? x.Name, x.FinancialInstitution?.Kind == InstitutionKind.Bank ? "Банк" : "Кошелёк", x.Currency, x.OpeningBalance + settlements.GetValueOrDefault(x.Id) - accountExpenses.GetValueOrDefault(x.Id) + movements.GetValueOrDefault(x.Id))).OrderBy(x => x.Company).ThenBy(x => x.Account).ThenBy(x => x.Currency).ToList();

        CompanyLabels = JsonSerializer.Serialize(CompanyResults.Select(x => x.Company));
        CompanyValues = JsonSerializer.Serialize(CompanyResults.Select(x => x.Net));
        OrientLabels = JsonSerializer.Serialize(OrientTurnovers.Select(x => $"{x.Direction} {x.Pair}"));
        OrientValues = JsonSerializer.Serialize(OrientTurnovers.Select(x => x.UsdtAmount));
    }
}
