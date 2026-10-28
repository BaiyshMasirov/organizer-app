using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace organaizer.Infrastructure.Migrations;

[DbContext(typeof(FinanceDbContext))]
[Migration("20261028090000_AnchorMrbRubBalanceToSeptember")]
public sealed class AnchorMrbRubBalanceToSeptember : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // The imported 05.08.2026 operation remains visible as historical reference,
        // but A&A starts reconciling the MRB RUB account from 01.09.2026 at zero.
        migrationBuilder.Sql("""
            UPDATE "Accounts"
            SET "OpeningBalanceAt" = TIMESTAMPTZ '2026-08-31 19:00:00+00'
            WHERE "Id" = '9aa8ec3b-d0bc-4937-8018-9722201ab738'
              AND "OpeningBalanceAt" IS NULL;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE "Accounts"
            SET "OpeningBalanceAt" = NULL
            WHERE "Id" = '9aa8ec3b-d0bc-4937-8018-9722201ab738'
              AND "OpeningBalanceAt" = TIMESTAMPTZ '2026-08-31 19:00:00+00';
            """);
    }
}
