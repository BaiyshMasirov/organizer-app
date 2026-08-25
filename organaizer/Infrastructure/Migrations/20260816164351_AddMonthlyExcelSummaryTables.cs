using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace organaizer.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMonthlyExcelSummaryTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MonthlyBalanceSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Period = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Currency = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    OpeningAmount = table.Column<decimal>(type: "numeric(24,8)", precision: 24, scale: 8, nullable: false),
                    ClosingAmount = table.Column<decimal>(type: "numeric(24,8)", precision: 24, scale: 8, nullable: false),
                    OpeningEquivalentUsdt = table.Column<decimal>(type: "numeric(24,8)", precision: 24, scale: 8, nullable: false),
                    ClosingEquivalentUsdt = table.Column<decimal>(type: "numeric(24,8)", precision: 24, scale: 8, nullable: false),
                    ImportKey = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MonthlyBalanceSnapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MonthlyExpenseTotals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Period = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Currency = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(24,8)", precision: 24, scale: 8, nullable: false),
                    EquivalentUsdt = table.Column<decimal>(type: "numeric(24,8)", precision: 24, scale: 8, nullable: false),
                    ImportKey = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MonthlyExpenseTotals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MonthlyPurchaseTotals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Period = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Pair = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ReceivedAmount = table.Column<decimal>(type: "numeric(24,8)", precision: 24, scale: 8, nullable: false),
                    ReceivedCurrency = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    GivenAmount = table.Column<decimal>(type: "numeric(24,8)", precision: 24, scale: 8, nullable: false),
                    GivenCurrency = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    ImportKey = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MonthlyPurchaseTotals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MonthlySaleTotals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Period = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Pair = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ReceivedAmount = table.Column<decimal>(type: "numeric(24,8)", precision: 24, scale: 8, nullable: false),
                    ReceivedCurrency = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    GivenAmount = table.Column<decimal>(type: "numeric(24,8)", precision: 24, scale: 8, nullable: false),
                    GivenCurrency = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    ImportKey = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MonthlySaleTotals", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MonthlyBalanceSnapshots_ImportKey",
                table: "MonthlyBalanceSnapshots",
                column: "ImportKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MonthlyExpenseTotals_ImportKey",
                table: "MonthlyExpenseTotals",
                column: "ImportKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MonthlyPurchaseTotals_ImportKey",
                table: "MonthlyPurchaseTotals",
                column: "ImportKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MonthlySaleTotals_ImportKey",
                table: "MonthlySaleTotals",
                column: "ImportKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MonthlyBalanceSnapshots");

            migrationBuilder.DropTable(
                name: "MonthlyExpenseTotals");

            migrationBuilder.DropTable(
                name: "MonthlyPurchaseTotals");

            migrationBuilder.DropTable(
                name: "MonthlySaleTotals");
        }
    }
}
