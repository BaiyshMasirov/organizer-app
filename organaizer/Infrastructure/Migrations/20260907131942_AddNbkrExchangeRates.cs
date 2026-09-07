using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace organaizer.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNbkrExchangeRates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NbkrExchangeRates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Currency = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    EffectiveAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Nominal = table.Column<decimal>(type: "numeric(30,15)", precision: 30, scale: 15, nullable: false),
                    ValueInKgs = table.Column<decimal>(type: "numeric(30,15)", precision: 30, scale: 15, nullable: false),
                    Feed = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SyncedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NbkrExchangeRates", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NbkrExchangeRates_Currency_EffectiveAt",
                table: "NbkrExchangeRates",
                columns: new[] { "Currency", "EffectiveAt" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NbkrExchangeRates");
        }
    }
}
