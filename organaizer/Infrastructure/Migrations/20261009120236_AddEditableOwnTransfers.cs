using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace organaizer.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEditableOwnTransfers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Note",
                table: "AccountMovements",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(300)",
                oldMaxLength: 300,
                oldNullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsCancelled",
                table: "AccountMovements",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "Revision",
                table: "AccountMovements",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "TransferRevisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    TransferId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    Action = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Actor = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    ChangedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FromAccount = table.Column<string>(type: "character varying(330)", maxLength: 330, nullable: false),
                    ToAccount = table.Column<string>(type: "character varying(330)", maxLength: 330, nullable: false),
                    Currency = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(24,8)", precision: 24, scale: 8, nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsCancelled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransferRevisions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TransferRevisions_TransferId_Revision",
                table: "TransferRevisions",
                columns: new[] { "TransferId", "Revision" },
                unique: true);

            // Materialize existing paired transfers without adding any cash movements.
            migrationBuilder.Sql("""
                WITH valid_groups AS (
                    SELECT "GroupId" FROM "AccountMovements"
                    GROUP BY "GroupId"
                    HAVING COUNT(*) = 2 AND MIN("Kind") = 0 AND MAX("Kind") = 0
                       AND SUM("Amount") = 0 AND MIN("Amount") < 0 AND MAX("Amount") > 0
                       AND COUNT(DISTINCT "CompanyId") = 1 AND COUNT(DISTINCT "AccountId") = 2
                       AND COUNT(DISTINCT "Currency") = 1 AND COUNT(DISTINCT "OccurredAt") = 1
                )
                INSERT INTO "Operations" ("Id", "CompanyId", "TypeCode", "OccurredAt", "CreatedAt",
                    "SellCurrency", "SellAmount", "BuyCurrency", "BuyAmount", "FeeAmount", "FeeCurrency",
                    "BaseCurrencyProfit", "Status", "Note", "SourceAccount", "DestinationAccount")
                SELECT outgoing."GroupId", outgoing."CompanyId", 'OWN_TRANSFER', outgoing."OccurredAt", outgoing."OccurredAt",
                    outgoing."Currency", -outgoing."Amount", incoming."Currency", incoming."Amount", 0, outgoing."Currency",
                    0, 3, outgoing."Note", source."Name", destination."Name"
                FROM valid_groups groups
                JOIN "AccountMovements" outgoing ON outgoing."GroupId" = groups."GroupId" AND outgoing."Amount" < 0
                JOIN "AccountMovements" incoming ON incoming."GroupId" = groups."GroupId" AND incoming."Amount" > 0
                JOIN "Accounts" source ON source."Id" = outgoing."AccountId" AND source."CompanyId" = outgoing."CompanyId" AND source."Currency" = outgoing."Currency"
                JOIN "Accounts" destination ON destination."Id" = incoming."AccountId" AND destination."CompanyId" = incoming."CompanyId" AND destination."Currency" = incoming."Currency"
                ON CONFLICT ("Id") DO NOTHING;

                INSERT INTO "TransferRevisions" ("Id", "CompanyId", "TransferId", "Revision", "Action", "Actor", "ChangedAt",
                    "OccurredAt", "FromAccount", "ToAccount", "Currency", "Amount", "Note", "IsCancelled")
                SELECT "Id", "CompanyId", "Id", 0, 'Существующий перевод', 'Миграция (автор неизвестен)', CURRENT_TIMESTAMP,
                    "OccurredAt", "SourceAccount", "DestinationAccount", "SellCurrency", "SellAmount", "Note", false
                FROM "Operations" WHERE "TypeCode" = 'OWN_TRANSFER'
                ON CONFLICT ("TransferId", "Revision") DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM "TransferRevisions" WHERE "Revision" > 0) THEN
                        RAISE EXCEPTION 'Нельзя откатить миграцию после изменения переводов: история будет потеряна';
                    END IF;
                END $$;
                DELETE FROM "Operations" WHERE "TypeCode" = 'OWN_TRANSFER';
                """);
            migrationBuilder.DropTable(
                name: "TransferRevisions");

            migrationBuilder.DropColumn(
                name: "IsCancelled",
                table: "AccountMovements");

            migrationBuilder.DropColumn(
                name: "Revision",
                table: "AccountMovements");

            migrationBuilder.AlterColumn<string>(
                name: "Note",
                table: "AccountMovements",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);
        }
    }
}
