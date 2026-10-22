using Microsoft.EntityFrameworkCore;
using organaizer.Domain;

namespace organaizer.Infrastructure;

public sealed class ActiveCompany(IHttpContextAccessor accessor, FinanceDbContext db)
{
    public const string SessionKey = "ActiveCompanyId";
    public Guid? Id => Guid.TryParse(accessor.HttpContext?.Session.GetString(SessionKey), out var id) ? id : null;
    public Guid RequiredId => Id ?? throw new InvalidOperationException("Компания не выбрана");
    public Task<Company?> GetAsync() => Id is { } id ? db.Companies.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id) : Task.FromResult<Company?>(null);
    public void Select(Guid id) => accessor.HttpContext!.Session.SetString(SessionKey, id.ToString());
}

public static class BalanceCalculator
{
    public static async Task<decimal> GetAsync(FinanceDbContext db, Guid accountId, CancellationToken ct = default)
    {
        var account = await db.Accounts.AsNoTracking().SingleAsync(x => x.Id == accountId, ct);
        return (await GetManyAsync(db,[account],ct))[accountId];
    }

    public static async Task<Dictionary<Guid,decimal>> GetManyAsync(FinanceDbContext db, IReadOnlyCollection<MoneyAccount> accounts,
        CancellationToken ct=default, bool ignoreQueryFilters=false)
    {
        var ids=accounts.Select(x=>x.Id).ToList();
        var aq=ignoreQueryFilters?db.Accounts.IgnoreQueryFilters():db.Accounts;
        var sq=ignoreQueryFilters?db.Settlements.IgnoreQueryFilters():db.Settlements;
        var eq=ignoreQueryFilters?db.Expenses.IgnoreQueryFilters():db.Expenses;
        var mq=ignoreQueryFilters?db.AccountMovements.IgnoreQueryFilters():db.AccountMovements;
        var settlements=await (from s in sq.AsNoTracking() join a in aq on s.AccountId equals a.Id
            where ids.Contains(a.Id) && s.Operation!.Status!=OperationStatus.Cancelled && (!a.OpeningBalanceAt.HasValue || s.OccurredAt>=a.OpeningBalanceAt.Value)
            group s by a.Id into g select new {Id=g.Key,Amount=g.Sum(x=>x.Amount)}).ToDictionaryAsync(x=>x.Id,x=>x.Amount,ct);
        var expenses=await (from e in eq.AsNoTracking() join a in aq on e.AccountId equals a.Id
            where ids.Contains(a.Id) && (!a.OpeningBalanceAt.HasValue || e.OccurredAt>=a.OpeningBalanceAt.Value)
            group e by a.Id into g select new {Id=g.Key,Amount=g.Sum(x=>x.Amount)}).ToDictionaryAsync(x=>x.Id,x=>x.Amount,ct);
        var movements=await (from m in mq.AsNoTracking() join a in aq on m.AccountId equals a.Id
            where ids.Contains(a.Id) && !m.IsCancelled && (!a.OpeningBalanceAt.HasValue || m.OccurredAt>=a.OpeningBalanceAt.Value)
            group m by a.Id into g select new {Id=g.Key,Amount=g.Sum(x=>x.Amount)}).ToDictionaryAsync(x=>x.Id,x=>x.Amount,ct);
        return accounts.ToDictionary(x=>x.Id,x=>x.OpeningBalance+settlements.GetValueOrDefault(x.Id)-expenses.GetValueOrDefault(x.Id)+movements.GetValueOrDefault(x.Id));
    }
}
