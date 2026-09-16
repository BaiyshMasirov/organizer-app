using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace organaizer.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AllowMultipleAccountsPerInstitution : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Accounts_CompanyId_FinancialInstitutionId_Currency",
                table: "Accounts");

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_CompanyId_FinancialInstitutionId_Currency",
                table: "Accounts",
                columns: new[] { "CompanyId", "FinancialInstitutionId", "Currency" });

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_CompanyId_Name_Currency",
                table: "Accounts",
                columns: new[] { "CompanyId", "Name", "Currency" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Accounts_CompanyId_FinancialInstitutionId_Currency",
                table: "Accounts");

            migrationBuilder.DropIndex(
                name: "IX_Accounts_CompanyId_Name_Currency",
                table: "Accounts");

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_CompanyId_FinancialInstitutionId_Currency",
                table: "Accounts",
                columns: new[] { "CompanyId", "FinancialInstitutionId", "Currency" },
                unique: true);
        }
    }
}
