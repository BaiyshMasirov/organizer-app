using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace organaizer.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddManualExpenseUsdAmount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ManualUsdAmount",
                table: "Expenses",
                type: "numeric(24,8)",
                precision: 24,
                scale: 8,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ManualUsdAmount",
                table: "Expenses");
        }
    }
}
