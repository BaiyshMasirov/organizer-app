using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace organaizer.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ImportHistoricalExcelData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DestinationAccount",
                table: "Operations",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ExchangeRate",
                table: "Operations",
                type: "numeric(30,15)",
                precision: 30,
                scale: 15,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImportKey",
                table: "Operations",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceAccount",
                table: "Operations",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImportKey",
                table: "Expenses",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "HistoricalImportRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceKey = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    SourceFile = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    SourceSheet = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    SourceRow = table.Column<int>(type: "integer", nullable: false),
                    RecordType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    DataJson = table.Column<string>(type: "text", nullable: false),
                    ImportedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistoricalImportRecords", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Operations_ImportKey",
                table: "Operations",
                column: "ImportKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_ImportKey",
                table: "Expenses",
                column: "ImportKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HistoricalImportRecords_SourceKey",
                table: "HistoricalImportRecords",
                column: "SourceKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HistoricalImportRecords");

            migrationBuilder.DropIndex(
                name: "IX_Operations_ImportKey",
                table: "Operations");

            migrationBuilder.DropIndex(
                name: "IX_Expenses_ImportKey",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "DestinationAccount",
                table: "Operations");

            migrationBuilder.DropColumn(
                name: "ExchangeRate",
                table: "Operations");

            migrationBuilder.DropColumn(
                name: "ImportKey",
                table: "Operations");

            migrationBuilder.DropColumn(
                name: "SourceAccount",
                table: "Operations");

            migrationBuilder.DropColumn(
                name: "ImportKey",
                table: "Expenses");
        }
    }
}
