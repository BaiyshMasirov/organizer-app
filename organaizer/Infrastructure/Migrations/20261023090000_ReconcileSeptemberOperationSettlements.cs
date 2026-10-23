using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace organaizer.Infrastructure.Migrations;

[DbContext(typeof(FinanceDbContext))]
[Migration("20261023090000_ReconcileSeptemberOperationSettlements")]
public sealed class ReconcileSeptemberOperationSettlements : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // These two operations were edited before settlement synchronization was added.
        // Their operation headers contain the correct AED amount, while the single incoming
        // settlement still contains the amount calculated by the old exchange-rate formula.
        migrationBuilder.Sql("""
            UPDATE "Settlements" AS s
            SET "Amount" = o."BuyAmount",
                "Currency" = o."BuyCurrency",
                "OccurredAt" = o."OccurredAt",
                "Note" = 'Зачисление по операции'
            FROM "Operations" AS o
            WHERE s."OperationId" = o."Id"
              AND s."Amount" > 0
              AND o."Id" IN (
                  '59520e26-6096-47af-9270-32836651d6ff',
                  'df1efa0f-3570-444f-9c1f-894ee2bc00c0'
              );
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE "Settlements"
            SET "Amount" = CASE "OperationId"
                WHEN '59520e26-6096-47af-9270-32836651d6ff' THEN 164385.93814284
                WHEN 'df1efa0f-3570-444f-9c1f-894ee2bc00c0' THEN 4793146.83552677
                ELSE "Amount"
            END
            WHERE "Amount" > 0
              AND "OperationId" IN (
                  '59520e26-6096-47af-9270-32836651d6ff',
                  'df1efa0f-3570-444f-9c1f-894ee2bc00c0'
              );
            """);
    }
}
