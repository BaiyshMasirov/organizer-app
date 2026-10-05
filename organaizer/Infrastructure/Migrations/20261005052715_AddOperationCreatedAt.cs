using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace organaizer.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOperationCreatedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "Operations",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Operations");
        }
    }
}
