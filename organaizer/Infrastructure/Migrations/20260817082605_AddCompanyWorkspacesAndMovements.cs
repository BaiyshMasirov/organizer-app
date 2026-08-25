using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace organaizer.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyWorkspacesAndMovements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Accounts_CompanyId",
                table: "Accounts");

            migrationBuilder.DropIndex(
                name: "IX_Accounts_FinancialInstitutionId_Currency",
                table: "Accounts");

            migrationBuilder.CreateTable(
                name: "AccountMovements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(24,8)", precision: 24, scale: 8, nullable: false),
                    Currency = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    Note = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountMovements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccountMovements_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_CompanyId_FinancialInstitutionId_Currency",
                table: "Accounts",
                columns: new[] { "CompanyId", "FinancialInstitutionId", "Currency" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_FinancialInstitutionId",
                table: "Accounts",
                column: "FinancialInstitutionId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountMovements_AccountId_OccurredAt",
                table: "AccountMovements",
                columns: new[] { "AccountId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AccountMovements_GroupId",
                table: "AccountMovements",
                column: "GroupId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountMovements");

            migrationBuilder.DropIndex(
                name: "IX_Accounts_CompanyId_FinancialInstitutionId_Currency",
                table: "Accounts");

            migrationBuilder.DropIndex(
                name: "IX_Accounts_FinancialInstitutionId",
                table: "Accounts");

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_CompanyId",
                table: "Accounts",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_FinancialInstitutionId_Currency",
                table: "Accounts",
                columns: new[] { "FinancialInstitutionId", "Currency" },
                unique: true);
        }
    }
}
