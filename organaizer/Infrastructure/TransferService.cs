using Microsoft.EntityFrameworkCore;
using organaizer.Domain;

namespace organaizer.Infrastructure;

public sealed class TransferService(FinanceDbContext db)
{
    public const string TypeCode = "OWN_TRANSFER";
    public const string Title = "Перевод между своими счетами";
    public static DateTimeOffset Instant(DateTime date) => new DateTimeOffset(
        DateTime.SpecifyKind(date.Date, DateTimeKind.Unspecified), TimeSpan.FromHours(5)).ToUniversalTime();
    public static string Label(MoneyAccount account) => Pages.Balance.IndexModel.AccountLabel(account);

    public async Task<Guid> CreateAsync(Guid companyId, Guid fromId, Guid toId, decimal amount, DateTime date, string? note, string actor)
    {
        var (from,to) = await ValidateAsync(companyId, fromId, toId, amount, date, note);
        var available = await BalanceCalculator.GetAsync(db,from.Id);
        if (available < amount) throw new ArgumentException($"Недостаточно средств на счёте «{Label(from)}»: доступно {available:N2} {from.Currency}.");
        var id=Guid.NewGuid();
        var at=Instant(date);
        var operation=new TradeOperation { Id=id, CompanyId=companyId, TypeCode=TypeCode,
            OccurredAt=at, CreatedAt=DateTimeOffset.UtcNow, Status=OperationStatus.Settled,
            SellAmount=amount, BuyAmount=amount, SellCurrency=from.Currency, BuyCurrency=from.Currency,
            SourceAccount=from.Name, DestinationAccount=to.Name, Note=note?.Trim() };
        db.Operations.Add(operation);
        db.AccountMovements.AddRange(
            new AccountMovement { Id=Guid.NewGuid(), CompanyId=companyId, AccountId=from.Id, GroupId=id,
                Kind=AccountMovementKind.Transfer, OccurredAt=at, Amount=-amount, Currency=from.Currency, Note=operation.Note },
            new AccountMovement { Id=Guid.NewGuid(), CompanyId=companyId, AccountId=to.Id, GroupId=id,
                Kind=AccountMovementKind.Transfer, OccurredAt=at, Amount=amount, Currency=to.Currency, Note=operation.Note });
        Audit(operation,0,"Создан",actor);
        await db.SaveChangesAsync();
        return id;
    }

    public async Task UpdateAsync(Guid companyId, Guid id, int revision, Guid fromId, Guid toId, decimal amount, DateTime date, string? note, string actor)
    {
        var (operation,rows)=await LoadAsync(companyId,id,revision);
        if(operation.Status==OperationStatus.Cancelled) throw new ArgumentException("Отменённый перевод нельзя редактировать.");
        var (from,to)=await ValidateAsync(companyId,fromId,toId,amount,date,note);
        var available=await BalanceCalculator.GetAsync(db,from.Id)-rows.Where(x=>x.AccountId==from.Id).Sum(x=>x.Amount);
        if(available<amount) throw new ArgumentException($"Недостаточно средств на счёте «{Label(from)}»: доступно {available:N2} {from.Currency} после замены перевода.");
        var outgoing=rows.Single(x=>x.Amount<0);var incoming=rows.Single(x=>x.Amount>0);
        outgoing.AccountId=from.Id;outgoing.Amount=-amount;outgoing.Currency=from.Currency;
        incoming.AccountId=to.Id;incoming.Amount=amount;incoming.Currency=to.Currency;
        operation.OccurredAt=Instant(date);operation.SellAmount=amount;operation.BuyAmount=amount;
        operation.SellCurrency=from.Currency;operation.BuyCurrency=to.Currency;
        operation.SourceAccount=from.Name;operation.DestinationAccount=to.Name;operation.Note=note?.Trim();
        foreach(var row in rows) { row.OccurredAt=operation.OccurredAt;row.Note=operation.Note;row.Revision++; }
        Audit(operation,revision+1,"Изменён",actor);
        await db.SaveChangesAsync();
    }

    public async Task CancelAsync(Guid companyId, Guid id, int revision, string actor)
    {
        var (operation,rows)=await LoadAsync(companyId,id,revision);
        if(operation.Status==OperationStatus.Cancelled) return;
        operation.Status=OperationStatus.Cancelled;
        foreach(var row in rows) { row.IsCancelled=true;row.Revision++; }
        Audit(operation,revision+1,"Отменён",actor);
        await db.SaveChangesAsync();
    }

    private async Task<(TradeOperation,List<AccountMovement>)> LoadAsync(Guid companyId, Guid id, int revision)
    {
        var operation=await db.Operations.SingleOrDefaultAsync(x=>x.Id==id && x.CompanyId==companyId && x.TypeCode==TypeCode)
            ?? throw new ArgumentException("Перевод не найден.");
        var rows=await db.AccountMovements.Where(x=>x.GroupId==id && x.CompanyId==companyId && x.Kind==AccountMovementKind.Transfer).ToListAsync();
        if(rows.Count!=2 || rows.Count(x=>x.Amount<0)!=1 || rows.Count(x=>x.Amount>0)!=1 || rows.Sum(x=>x.Amount)!=0)
            throw new ArgumentException("Нарушена целостность перевода. Требуется сверка движений.");
        if(rows.Select(x=>x.AccountId).Distinct().Count()!=2 ||
            rows.Any(x=>x.Currency!=operation.SellCurrency || x.OccurredAt!=operation.OccurredAt ||
                Math.Abs(x.Amount)!=operation.SellAmount || x.IsCancelled!=(operation.Status==OperationStatus.Cancelled)))
            throw new ArgumentException("Движения не совпадают с записью перевода. Требуется сверка.");
        if(rows.Any(x=>x.Revision!=revision)) throw new DbUpdateConcurrencyException("Перевод уже изменён. Обновите страницу.");
        return (operation,rows);
    }

    private async Task<(MoneyAccount,MoneyAccount)> ValidateAsync(Guid companyId, Guid fromId, Guid toId, decimal amount, DateTime date, string? note)
    {
        if(fromId==toId || amount<=0 || amount>=10000000000000000m || date==default || note?.Length>500)
            throw new ArgumentException("Выберите два разных счёта, дату и положительную сумму; комментарий — до 500 символов.");
        var accounts=await db.Accounts.Include(x=>x.FinancialInstitution)
            .Where(x=>x.CompanyId==companyId && x.IsActive && (x.Id==fromId || x.Id==toId)).ToListAsync();
        var from=accounts.SingleOrDefault(x=>x.Id==fromId);var to=accounts.SingleOrDefault(x=>x.Id==toId);
        if(from is null || to is null || from.Currency!=to.Currency)
            throw new ArgumentException("Выберите активные счета своей компании в одной валюте.");
        return (from,to);
    }

    private void Audit(TradeOperation operation,int revision,string action,string actor) => db.TransferRevisions.Add(new TransferRevision {
        Id=Guid.NewGuid(),CompanyId=operation.CompanyId,TransferId=operation.Id,Revision=revision,
        Action=action,Actor=actor.Length>180?actor[..180]:actor,ChangedAt=DateTimeOffset.UtcNow,OccurredAt=operation.OccurredAt,
        FromAccount=operation.SourceAccount!,ToAccount=operation.DestinationAccount!,Amount=operation.SellAmount,
        Currency=operation.SellCurrency,Note=operation.Note,IsCancelled=operation.Status==OperationStatus.Cancelled });
}
