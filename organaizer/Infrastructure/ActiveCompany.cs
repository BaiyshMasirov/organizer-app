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
        var settlements = await db.Settlements.AsNoTracking().Where(x => x.AccountId == accountId && x.Operation!.Status != OperationStatus.Cancelled).SumAsync(x => x.Amount, ct);
        var expenses = await db.Expenses.AsNoTracking().Where(x => x.AccountId == accountId).SumAsync(x => x.Amount, ct);
        var movements = await db.AccountMovements.AsNoTracking().Where(x => x.AccountId == accountId).SumAsync(x => x.Amount, ct);
        return account.OpeningBalance + settlements - expenses + movements;
    }
}
