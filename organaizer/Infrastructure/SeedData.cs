using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using organaizer.Domain;

namespace organaizer.Infrastructure;

public static class SeedData
{
    public static async Task InitializeAsync(FinanceDbContext db, UserManager<IdentityUser> users, RoleManager<IdentityRole> roles)
    {
        await db.Database.MigrateAsync();
        const string adminRole = "Administrator";
        if (!await roles.RoleExistsAsync(adminRole))
            await EnsureSucceeded(roles.CreateAsync(new IdentityRole(adminRole)));
        var admin = await users.FindByNameAsync("admin");
        if (admin is null)
        {
            admin = new IdentityUser { UserName = "admin", Email = "admin@local", EmailConfirmed = true };
            await EnsureSucceeded(users.CreateAsync(admin, "inFO@)20"));
        }
        if (!await users.IsInRoleAsync(admin, adminRole))
            await EnsureSucceeded(users.AddToRoleAsync(admin, adminRole));
        if (!await db.Companies.AnyAsync())
        {
            var broker = new Company { Id=Guid.NewGuid(), Name="Кыргызстан — Криптообменник", Kind=CompanyKind.Broker };
            var lp = new Company { Id=Guid.NewGuid(), Name="Dubai — Liquidity Provider", Kind=CompanyKind.LiquidityProvider };
            db.Companies.AddRange(broker, lp);
            db.Accounts.AddRange(
                new MoneyAccount { Id=Guid.NewGuid(), CompanyId=lp.Id, Name="Банк USD", Kind=AccountKind.Bank, Currency="USD" },
                new MoneyAccount { Id=Guid.NewGuid(), CompanyId=lp.Id, Name="Банк AED", Kind=AccountKind.Bank, Currency="AED" },
                new MoneyAccount { Id=Guid.NewGuid(), CompanyId=lp.Id, Name="Биржа USDT", Kind=AccountKind.Exchange, Currency="USDT" },
                new MoneyAccount { Id=Guid.NewGuid(), CompanyId=lp.Id, Name="Кошелек RUB", Kind=AccountKind.CryptoWallet, Currency="RUB" });
            await db.SaveChangesAsync();
        }

        var existingCurrencies = await db.Currencies.Select(x => x.Code).ToListAsync();
        foreach (var item in SeedCatalog.Currencies.Where(x => !existingCurrencies.Contains(x.Code)))
            db.Currencies.Add(new Currency { Code=item.Code, Name=item.Name, Symbol=item.Symbol, Precision=item.Precision });
        var existingInstitutions=(await db.FinancialInstitutions.Select(x=>x.Name).ToListAsync()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach(var item in SeedCatalog.Institutions.Where(x=>existingInstitutions.Add(x.Name)))
            db.FinancialInstitutions.Add(new FinancialInstitution{Id=Guid.NewGuid(),Name=item.Name,Kind=item.Kind});

        var companies = await db.Companies.ToListAsync();
        var brokerCompany = companies.Single(x => x.Kind == CompanyKind.Broker);
        var liquidityCompany = companies.Single(x => x.Kind == CompanyKind.LiquidityProvider);
        brokerCompany.Name = "Orient Capital";
        liquidityCompany.Name = "A&A Liquidity";
        await AddClients(db, brokerCompany.Id, SeedCatalog.BrokerClients);
        await AddClients(db, liquidityCompany.Id, SeedCatalog.LiquidityClients);
        await db.SaveChangesAsync();
    }

    private static async Task AddClients(FinanceDbContext db, Guid companyId, IEnumerable<string> names)
    {
        var existing = (await db.Counterparties.Where(x => x.CompanyId == companyId).Select(x => x.Name).ToListAsync())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var name in names.Where(x => existing.Add(x)))
            db.Counterparties.Add(new Counterparty { Id=Guid.NewGuid(), CompanyId=companyId, Name=name, Kind=CounterpartyKind.Client });
    }

    private static async Task EnsureSucceeded(Task<IdentityResult> operation)
    {
        var result = await operation;
        if (!result.Succeeded) throw new InvalidOperationException(string.Join("; ", result.Errors.Select(x => x.Description)));
    }
}
