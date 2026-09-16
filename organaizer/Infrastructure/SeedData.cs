using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using organaizer.Domain;

namespace organaizer.Infrastructure;

public static class SeedData
{
    public static async Task InitializeAsync(FinanceDbContext db, UserManager<IdentityUser> users, RoleManager<IdentityRole> roles)
    {
        await db.Database.MigrateAsync();
        const string adminRole = "Administrator";
        const string superAdminRole = AppPermissions.SuperAdminRole;
        if (!await roles.RoleExistsAsync(adminRole))
            await EnsureSucceeded(roles.CreateAsync(new IdentityRole(adminRole)));
        if (!await roles.RoleExistsAsync(superAdminRole))
            await EnsureSucceeded(roles.CreateAsync(new IdentityRole(superAdminRole)));
        var admin = await users.FindByNameAsync("admin");
        if (admin is null)
        {
            admin = new IdentityUser { UserName = "admin", Email = "admin@local", EmailConfirmed = true };
            await EnsureSucceeded(users.CreateAsync(admin, "inFO@)20"));
        }
        if (!await users.IsInRoleAsync(admin, adminRole))
            await EnsureSucceeded(users.AddToRoleAsync(admin, adminRole));
        var adminClaims = await users.GetClaimsAsync(admin);
        foreach (var permission in AppPermissions.All.Where(x => !adminClaims.Any(c => c.Type == AppPermissions.ClaimType && c.Value == x)))
            await EnsureSucceeded(users.AddClaimAsync(admin, new Claim(AppPermissions.ClaimType, permission)));
        var superAdmin = await users.FindByNameAsync("superadmin");
        if (superAdmin is null)
        {
            superAdmin = new IdentityUser { UserName = "superadmin", Email = "superadmin@local", EmailConfirmed = true };
            await EnsureSucceeded(users.CreateAsync(superAdmin, "12345678"));
        }
        if (!await users.IsInRoleAsync(superAdmin, superAdminRole))
            await EnsureSucceeded(users.AddToRoleAsync(superAdmin, superAdminRole));
        await EnsureNbkrBaselineAsync(db);
        await EnsureAaUsdtRateAsync(db);
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

    private static async Task EnsureAaUsdtRateAsync(FinanceDbContext db)
    {
        var rate = await db.ExchangeRates.SingleOrDefaultAsync(x => x.ImportKey == AaExchangeRateService.UsdtImportKey);
        if (rate is null)
        {
            db.ExchangeRates.Add(new ExchangeRate
            {
                Id = Guid.NewGuid(), Currency = "USDT",
                EffectiveAt = new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero),
                SourceOrder = 1_900_000, RateToUsd = 1m / AaExchangeRateService.UsdToUsdt,
                Note = "Постоянный курс A&A: 1 USD = 1,003 USDT",
                ImportKey = AaExchangeRateService.UsdtImportKey
            });
            await db.SaveChangesAsync();
        }
        else if (rate.RateToUsd != 1m / AaExchangeRateService.UsdToUsdt)
        {
            rate.RateToUsd = 1m / AaExchangeRateService.UsdToUsdt;
            rate.Note = "Постоянный курс A&A: 1 USD = 1,003 USDT";
            await db.SaveChangesAsync();
        }
    }

    private static async Task EnsureNbkrBaselineAsync(FinanceDbContext db)
    {
        // Official NBKR rates effective on 08.09.2026. Keep a local baseline because
        // the NBKR host can be temporarily unreachable from the production server.
        var effectiveAt = new DateTimeOffset(2026, 9, 8, 0, 0, 0, TimeSpan.Zero);
        var officialRates = new[]
        {
            new { Currency = "USD", Nominal = 1m, ValueInKgs = 87.4500m, Feed = "daily" },
            new { Currency = "AED", Nominal = 1m, ValueInKgs = 23.8112m, Feed = "weekly" }
        };
        var existing = await db.NbkrExchangeRates
            .Where(x => x.EffectiveAt == effectiveAt)
            .Select(x => x.Currency)
            .ToListAsync();
        foreach (var rate in officialRates.Where(x => !existing.Contains(x.Currency)))
            db.NbkrExchangeRates.Add(new NbkrExchangeRate
            {
                Id = Guid.NewGuid(),
                Currency = rate.Currency,
                EffectiveAt = effectiveAt,
                Nominal = rate.Nominal,
                ValueInKgs = rate.ValueInKgs,
                Feed = rate.Feed
            });
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
