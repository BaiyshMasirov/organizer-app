using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace organaizer.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBalanceInstitutions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "FinancialInstitutionId",
                table: "Accounts",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_FinancialInstitutionId_Currency",
                table: "Accounts",
                columns: new[] { "FinancialInstitutionId", "Currency" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Accounts_FinancialInstitutions_FinancialInstitutionId",
                table: "Accounts",
                column: "FinancialInstitutionId",
                principalTable: "FinancialInstitutions",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Accounts_FinancialInstitutions_FinancialInstitutionId",
                table: "Accounts");

            migrationBuilder.DropIndex(
                name: "IX_Accounts_FinancialInstitutionId_Currency",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "FinancialInstitutionId",
                table: "Accounts");
        }
    }
}
