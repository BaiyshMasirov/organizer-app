using Microsoft.EntityFrameworkCore;
using organaizer.Domain;

namespace organaizer.Infrastructure;

public sealed class LoanService(FinanceDbContext db)
{
    public const string ReceivedTypeCode = "LOAN_RECEIVED";
    public const string RepaidTypeCode = "LOAN_REPAID";

    public async Task<Guid> CreateAsync(Guid companyId, Guid counterpartyId, Guid accountId,
        string typeCode, decimal amount, DateTime occurredAt, DateTime? dueAt, string? note,
        CancellationToken ct = default)
    {
        if (typeCode is not ReceivedTypeCode and not RepaidTypeCode)
            throw new ArgumentException("Выберите получение или возврат займа.");
        if (amount <= 0 || amount >= 10000000000000000m || occurredAt == default)
            throw new ArgumentException("Укажите дату и положительную сумму займа.");
        if (note?.Length > 500) throw new ArgumentException("Комментарий должен быть не длиннее 500 символов.");

        var isAa = await db.Companies.AsNoTracking().AnyAsync(x =>
            x.Id == companyId && x.Kind == CompanyKind.LiquidityProvider, ct);
        if (!isAa) throw new ArgumentException("Журнал займов доступен только для компании A&A.");

        var counterparty = await db.Counterparties.AsNoTracking().SingleOrDefaultAsync(x =>
            x.Id == counterpartyId && x.CompanyId == companyId && x.IsActive, ct)
            ?? throw new ArgumentException("Выберите действующего контрагента своей компании.");
        var account = await db.Accounts.AsNoTracking().Include(x => x.FinancialInstitution)
            .SingleOrDefaultAsync(x => x.Id == accountId && x.CompanyId == companyId && x.IsActive, ct)
            ?? throw new ArgumentException("Выберите действующий счёт своей компании.");

        var received = typeCode == ReceivedTypeCode;
        var at = new DateTimeOffset(DateTime.SpecifyKind(occurredAt.Date, DateTimeKind.Utc));
        var operation = new TradeOperation
        {
            Id = Guid.NewGuid(), CompanyId = companyId, CounterpartyId = counterparty.Id,
            TypeCode = typeCode, CreatedAt = DateTimeOffset.UtcNow, OccurredAt = at,
            DueAt = dueAt.HasValue ? new DateTimeOffset(DateTime.SpecifyKind(dueAt.Value.Date, DateTimeKind.Utc)) : null,
            SellCurrency = account.Currency, SellAmount = received ? 0 : amount,
            BuyCurrency = account.Currency, BuyAmount = received ? amount : 0,
            FeeCurrency = "USD", BaseCurrencyProfit = 0, ExchangeRate = 1,
            Status = OperationStatus.Settled, Note = note?.Trim(),
            SourceAccount = received ? null : account.Name,
            DestinationAccount = received ? account.Name : null
        };
        db.Operations.Add(operation);
        db.Settlements.Add(new Settlement
        {
            Id = Guid.NewGuid(), OperationId = operation.Id, AccountId = account.Id,
            OccurredAt = at, Amount = received ? amount : -amount, Currency = account.Currency,
            Note = received ? $"Получение займа от {counterparty.Name}" : $"Возврат займа: {counterparty.Name}"
        });
        await db.SaveChangesAsync(ct);
        return operation.Id;
    }
}
