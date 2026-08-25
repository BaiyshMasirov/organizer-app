using System.ComponentModel.DataAnnotations;

namespace organaizer.Domain;

public enum CompanyKind { Broker, LiquidityProvider }
public enum AccountKind { Bank, Exchange, CryptoWallet, Cash, Counterparty }
public enum CounterpartyKind { Client, Partner, Agent }
public enum AgentRewardKind { None, TurnoverPercent, ProfitSharePercent }
public enum OperationStatus { Draft, Open, PartiallySettled, Settled, Cancelled }
public enum InstitutionKind { Bank, Exchange, Wallet, PaymentSystem, Other }
public enum AccountMovementKind { Transfer, Conversion }

public sealed class Company
{
    public Guid Id { get; set; }
    [MaxLength(160)] public required string Name { get; set; }
    public CompanyKind Kind { get; set; }
    [MaxLength(3)] public string BaseCurrency { get; set; } = "USD";
}

public sealed class MoneyAccount
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public Company? Company { get; set; }
    public Guid? FinancialInstitutionId { get; set; }
    public FinancialInstitution? FinancialInstitution { get; set; }
    [MaxLength(160)] public required string Name { get; set; }
    public AccountKind Kind { get; set; }
    [MaxLength(5)] public required string Currency { get; set; }
    public decimal OpeningBalance { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class Counterparty
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    [MaxLength(180)] public required string Name { get; set; }
    public CounterpartyKind Kind { get; set; }
    public AgentRewardKind RewardKind { get; set; }
    public decimal RewardRate { get; set; }
    public Guid? AgentId { get; set; }
    [MaxLength(80)] public string? Code { get; set; }
    [MaxLength(500)] public string? Note { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class Currency
{
    [Key, MaxLength(5)] public required string Code { get; set; }
    [MaxLength(80)] public required string Name { get; set; }
    [MaxLength(8)] public string Symbol { get; set; } = "";
    public int Precision { get; set; } = 2;
    public bool IsActive { get; set; } = true;
}

public sealed class FinancialInstitution
{
    public Guid Id { get; set; }
    [MaxLength(160)] public required string Name { get; set; }
    public InstitutionKind Kind { get; set; }
    public bool IsActive { get; set; } = true;
    [MaxLength(300)] public string? Note { get; set; }
}

public sealed class TradeOperation
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public Guid? CounterpartyId { get; set; }
    public Counterparty? Counterparty { get; set; }
    [MaxLength(40)] public required string TypeCode { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public DateTimeOffset? DueAt { get; set; }
    [MaxLength(5)] public required string SellCurrency { get; set; }
    public decimal SellAmount { get; set; }
    [MaxLength(5)] public required string BuyCurrency { get; set; }
    public decimal BuyAmount { get; set; }
    public decimal FeeAmount { get; set; }
    [MaxLength(5)] public string FeeCurrency { get; set; } = "USD";
    public decimal BaseCurrencyProfit { get; set; }
    public OperationStatus Status { get; set; } = OperationStatus.Open;
    [MaxLength(500)] public string? Note { get; set; }
    public decimal? ExchangeRate { get; set; }
    [MaxLength(160)] public string? SourceAccount { get; set; }
    [MaxLength(160)] public string? DestinationAccount { get; set; }
    [MaxLength(300)] public string? ImportKey { get; set; }
    public List<Settlement> Settlements { get; set; } = [];
}

public sealed class Settlement
{
    public Guid Id { get; set; }
    public Guid OperationId { get; set; }
    public TradeOperation? Operation { get; set; }
    public Guid AccountId { get; set; }
    public MoneyAccount? Account { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public decimal Amount { get; set; }
    [MaxLength(5)] public required string Currency { get; set; }
    [MaxLength(300)] public string? Note { get; set; }
}

public sealed class AccountMovement
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public Guid AccountId { get; set; }
    public MoneyAccount? Account { get; set; }
    public Guid GroupId { get; set; }
    public AccountMovementKind Kind { get; set; }
    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
    public decimal Amount { get; set; }
    [MaxLength(5)] public required string Currency { get; set; }
    [MaxLength(300)] public string? Note { get; set; }
}

public sealed class Expense
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public Guid AccountId { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    [MaxLength(120)] public required string Category { get; set; }
    public decimal Amount { get; set; }
    [MaxLength(5)] public required string Currency { get; set; }
    public decimal BaseCurrencyAmount { get; set; }
    [MaxLength(300)] public string? Note { get; set; }
    [MaxLength(300)] public string? ImportKey { get; set; }
}

public sealed class ExchangeRate
{
    public Guid Id { get; set; }
    [MaxLength(5)] public required string Currency { get; set; }
    public DateTimeOffset EffectiveAt { get; set; }
    public int SourceOrder { get; set; }
    /// <summary>Стоимость одной единицы валюты в USDT.</summary>
    public decimal RateToUsd { get; set; }
    [MaxLength(300)] public string? Note { get; set; }
    [MaxLength(300)] public string? ImportKey { get; set; }
}

public sealed class MonthlyCurrencyResult
{
    public Guid Id { get; set; }
    public DateTimeOffset Period { get; set; }
    [MaxLength(5)] public required string Currency { get; set; }
    public decimal NetAmount { get; set; }
    public decimal EquivalentUsdt { get; set; }
    [MaxLength(300)] public required string ImportKey { get; set; }
}

public sealed class MonthlyPurchaseTotal
{
    public Guid Id { get; set; } public DateTimeOffset Period { get; set; }
    [MaxLength(20)] public required string Pair { get; set; }
    public decimal ReceivedAmount { get; set; } [MaxLength(5)] public required string ReceivedCurrency { get; set; }
    public decimal GivenAmount { get; set; } [MaxLength(5)] public required string GivenCurrency { get; set; }
    [MaxLength(300)] public required string ImportKey { get; set; }
}

public sealed class MonthlySaleTotal
{
    public Guid Id { get; set; } public DateTimeOffset Period { get; set; }
    [MaxLength(20)] public required string Pair { get; set; }
    public decimal ReceivedAmount { get; set; } [MaxLength(5)] public required string ReceivedCurrency { get; set; }
    public decimal GivenAmount { get; set; } [MaxLength(5)] public required string GivenCurrency { get; set; }
    [MaxLength(300)] public required string ImportKey { get; set; }
}

public sealed class MonthlyExpenseTotal
{
    public Guid Id { get; set; } public DateTimeOffset Period { get; set; }
    [MaxLength(5)] public required string Currency { get; set; }
    public decimal Amount { get; set; } public decimal EquivalentUsdt { get; set; }
    [MaxLength(300)] public required string ImportKey { get; set; }
}

public sealed class MonthlyBalanceSnapshot
{
    public Guid Id { get; set; } public DateTimeOffset Period { get; set; }
    [MaxLength(5)] public required string Currency { get; set; }
    public decimal OpeningAmount { get; set; } public decimal ClosingAmount { get; set; }
    public decimal OpeningEquivalentUsdt { get; set; } public decimal ClosingEquivalentUsdt { get; set; }
    [MaxLength(300)] public required string ImportKey { get; set; }
}

public sealed class HistoricalImportRecord
{
    public Guid Id { get; set; }
    [MaxLength(300)] public required string SourceKey { get; set; }
    [MaxLength(180)] public required string SourceFile { get; set; }
    [MaxLength(120)] public required string SourceSheet { get; set; }
    public int SourceRow { get; set; }
    [MaxLength(40)] public required string RecordType { get; set; }
    public required string DataJson { get; set; }
    public DateTimeOffset ImportedAt { get; set; } = DateTimeOffset.UtcNow;
}

public static class OperationTypes
{
    public static readonly IReadOnlyDictionary<string, string> All = new Dictionary<string, string>
    {
        ["BUY_USDT_USD"]="Покупка USDT/USD", ["SELL_USDT_USD"]="Продажа USDT/USD",
        ["BUY_USDT_AED"]="Покупка USDT/AED", ["SELL_USDT_AED"]="Продажа USDT/AED",
        ["BUY_USDT_RUB"]="Покупка USDT/RUB", ["SELL_USDT_RUB"]="Продажа USDT/RUB",
        ["BUY_USD_RUB"]="Покупка USD/RUB", ["SELL_USD_RUB"]="Продажа USD/RUB",
        ["CREDIT_USDT"]="Кредит USDT/USDT", ["LIQUIDITY_USDT"]="Ликвидность USDT/USDT",
        ["SELL_USD_AED"]="Конвертация (продажа) USD/AED", ["BUY_USD_AED"]="Конвертация (покупка) USD/AED"
    };
    public static (string Sell, string Buy) Pair(string code) => code switch
    {
        "BUY_USDT_USD" => ("USD","USDT"), "SELL_USDT_USD" => ("USDT","USD"),
        "BUY_USDT_AED" => ("AED","USDT"), "SELL_USDT_AED" => ("USDT","AED"),
        "BUY_USDT_RUB" => ("RUB","USDT"), "SELL_USDT_RUB" => ("USDT","RUB"),
        "BUY_USD_RUB" => ("RUB","USD"), "SELL_USD_RUB" => ("USD","RUB"),
        "SELL_USD_AED" => ("USD","AED"), "BUY_USD_AED" => ("AED","USD"),
        _ => ("USDT","USDT")
    };
    public static string PairLabel(string code){var p=Pair(code);return $"{p.Sell}/{p.Buy}";}
}

public static class OperationStatuses
{
    public const string Created = "created";
    public const string Completed = "completed";
    public const string Cancelled = "cancelled";

    public static string Label(OperationStatus status) => status switch
    {
        OperationStatus.Settled => "Завершена",
        OperationStatus.Cancelled => "Отменена",
        _ => "Создана"
    };
}
