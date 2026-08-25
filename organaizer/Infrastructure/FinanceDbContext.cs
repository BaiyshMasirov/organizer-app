using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using organaizer.Domain;

namespace organaizer.Infrastructure;

public sealed class FinanceDbContext(DbContextOptions<FinanceDbContext> options, IHttpContextAccessor httpContextAccessor) : IdentityDbContext(options)
{
    private Guid? ActiveCompanyId => Guid.TryParse(httpContextAccessor.HttpContext?.Session.GetString(ActiveCompany.SessionKey), out var id) ? id : null;
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<MoneyAccount> Accounts => Set<MoneyAccount>();
    public DbSet<Counterparty> Counterparties => Set<Counterparty>();
    public DbSet<TradeOperation> Operations => Set<TradeOperation>();
    public DbSet<Settlement> Settlements => Set<Settlement>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<ExchangeRate> ExchangeRates => Set<ExchangeRate>();
    public DbSet<MonthlyCurrencyResult> MonthlyCurrencyResults => Set<MonthlyCurrencyResult>();
    public DbSet<MonthlyPurchaseTotal> MonthlyPurchaseTotals => Set<MonthlyPurchaseTotal>();
    public DbSet<MonthlySaleTotal> MonthlySaleTotals => Set<MonthlySaleTotal>();
    public DbSet<MonthlyExpenseTotal> MonthlyExpenseTotals => Set<MonthlyExpenseTotal>();
    public DbSet<MonthlyBalanceSnapshot> MonthlyBalanceSnapshots => Set<MonthlyBalanceSnapshot>();
    public DbSet<Currency> Currencies => Set<Currency>();
    public DbSet<HistoricalImportRecord> HistoricalImportRecords => Set<HistoricalImportRecord>();
    public DbSet<FinancialInstitution> FinancialInstitutions => Set<FinancialInstitution>();
    public DbSet<AccountMovement> AccountMovements => Set<AccountMovement>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);
        foreach (var type in b.Model.GetEntityTypes())
            foreach (var p in type.GetProperties().Where(x => x.ClrType == typeof(decimal)))
            {
                p.SetPrecision(24);
                p.SetScale(8);
            }
        b.Entity<Company>().HasIndex(x => x.Name).IsUnique();
        b.Entity<MoneyAccount>().HasQueryFilter(x => ActiveCompanyId == null || x.CompanyId == ActiveCompanyId);
        b.Entity<Counterparty>().HasQueryFilter(x => ActiveCompanyId == null || x.CompanyId == ActiveCompanyId);
        b.Entity<TradeOperation>().HasQueryFilter(x => ActiveCompanyId == null || x.CompanyId == ActiveCompanyId);
        b.Entity<Expense>().HasQueryFilter(x => ActiveCompanyId == null || x.CompanyId == ActiveCompanyId);
        b.Entity<AccountMovement>().HasQueryFilter(x => ActiveCompanyId == null || x.CompanyId == ActiveCompanyId);
        b.Entity<MoneyAccount>().HasIndex(x => new { x.CompanyId, x.FinancialInstitutionId, x.Currency }).IsUnique();
        b.Entity<AccountMovement>().HasIndex(x => new { x.AccountId, x.OccurredAt });
        b.Entity<AccountMovement>().HasIndex(x => x.GroupId);
        b.Entity<Settlement>().HasIndex(x => new { x.AccountId, x.OccurredAt });
        b.Entity<TradeOperation>().HasIndex(x => new { x.CompanyId, x.OccurredAt });
        b.Entity<Counterparty>().HasOne<Counterparty>().WithMany().HasForeignKey(x => x.AgentId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Counterparty>().HasIndex(x => new { x.CompanyId, x.Name }).IsUnique();
        b.Entity<TradeOperation>().HasIndex(x => x.ImportKey).IsUnique();
        b.Entity<TradeOperation>().Property(x => x.ExchangeRate).HasPrecision(30, 15);
        b.Entity<Expense>().HasIndex(x => x.ImportKey).IsUnique();
        b.Entity<ExchangeRate>().HasIndex(x => new { x.Currency, x.EffectiveAt });
        b.Entity<ExchangeRate>().HasIndex(x => x.ImportKey).IsUnique();
        b.Entity<ExchangeRate>().Property(x => x.RateToUsd).HasPrecision(30,15);
        b.Entity<MonthlyCurrencyResult>().HasIndex(x => new { x.Period, x.Currency }).IsUnique();
        b.Entity<MonthlyCurrencyResult>().HasIndex(x => x.ImportKey).IsUnique();
        b.Entity<MonthlyPurchaseTotal>().HasIndex(x=>x.ImportKey).IsUnique();
        b.Entity<MonthlySaleTotal>().HasIndex(x=>x.ImportKey).IsUnique();
        b.Entity<MonthlyExpenseTotal>().HasIndex(x=>x.ImportKey).IsUnique();
        b.Entity<MonthlyBalanceSnapshot>().HasIndex(x=>x.ImportKey).IsUnique();
        b.Entity<HistoricalImportRecord>().HasIndex(x => x.SourceKey).IsUnique();
        b.Entity<FinancialInstitution>().HasIndex(x => x.Name).IsUnique();
    }
}
