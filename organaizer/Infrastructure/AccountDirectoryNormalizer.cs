using Microsoft.EntityFrameworkCore;
using organaizer.Domain;

namespace organaizer.Infrastructure;

/// <summary>Объединяет валютные варианты названий банков, не смешивая отдельные платёжные маршруты.</summary>
public static class AccountDirectoryNormalizer
{
    private static readonly IReadOnlyDictionary<string, string> Aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["BAKAI AED"] = "BAKAI",
        ["BAKAI KGS"] = "BAKAI",
        ["BAKAI RUB"] = "BAKAI",
        ["BAKAI USD"] = "BAKAI",
        ["BAKAI USD Card"] = "BAKAI",
        ["BAKAI USD Трейдинг"] = "BAKAI",
        ["ВТБ RUB"] = "ВТБ",
        ["ВТБ USD"] = "ВТБ",
        ["Демир банк KGS"] = "Демир банк",
        ["Демир банк RUB"] = "Демир банк",
        ["Демир банк USD"] = "Демир банк",
        ["Октобанк KGS"] = "Октобанк",
        ["Октобанк RUB"] = "Октобанк",
        ["Октобанк USD"] = "Октобанк"
    };

    public static async Task NormalizeAsync(FinanceDbContext db)
    {
        var institutions = await db.FinancialInstitutions.ToListAsync();
        var byName = institutions.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);
        var accounts = await db.Accounts.IgnoreQueryFilters().ToListAsync();

        // This institution was entered by the user as a new account in the old UI.
        // Materialize it as an A&A balance account before folding the bank alias.
        if (byName.TryGetValue("BAKAI USD Трейдинг", out var tradingAlias))
        {
            var companyId = await db.Companies.Where(x => x.Kind == CompanyKind.LiquidityProvider).Select(x => x.Id).SingleAsync();
            if (!accounts.Any(x => x.CompanyId == companyId && x.Currency == "USD" && x.Name == "BAKAI USD Трейдинг"))
            {
                var canonical = EnsureInstitution("BAKAI", InstitutionKind.Bank);
                var account = new MoneyAccount
                {
                    Id=Guid.NewGuid(), CompanyId=companyId, FinancialInstitutionId=canonical.Id,
                    Name="BAKAI USD Трейдинг", Kind=AccountKind.Bank, Currency="USD", OpeningBalance=0
                };
                db.Accounts.Add(account);
                accounts.Add(account);
            }
        }

        foreach (var (aliasName, canonicalName) in Aliases)
        {
            if (!byName.TryGetValue(aliasName, out var alias)) continue;
            var canonical = EnsureInstitution(canonicalName, alias.Kind);
            foreach (var account in accounts.Where(x => x.FinancialInstitutionId == alias.Id))
                account.FinancialInstitutionId = canonical.Id;
            if (alias.Id != canonical.Id) alias.IsActive = false;
        }
        await db.SaveChangesAsync();

        FinancialInstitution EnsureInstitution(string name, InstitutionKind kind)
        {
            if (byName.TryGetValue(name, out var existing))
            {
                existing.IsActive = true;
                return existing;
            }
            var created = new FinancialInstitution { Id=Guid.NewGuid(), Name=name, Kind=kind, IsActive=true };
            byName[name] = created;
            institutions.Add(created);
            db.FinancialInstitutions.Add(created);
            return created;
        }
    }
}
