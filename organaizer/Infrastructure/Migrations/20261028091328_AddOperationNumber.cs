using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace organaizer.Infrastructure.Migrations;

public partial class AddOperationNumber : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateSequence<long>(
            name: "OperationNumberSequence");

        migrationBuilder.AddColumn<long>(
            name: "OperationNumber",
            table: "Operations",
            type: "bigint",
            nullable: false,
            defaultValueSql: "nextval('\"OperationNumberSequence\"')");

        migrationBuilder.Sql("""
            WITH numbered AS
            (
                SELECT "Id",
                       ROW_NUMBER() OVER
                       (
                           ORDER BY COALESCE("CreatedAt", "OccurredAt"), "OccurredAt", "Id"
                       ) AS number
                FROM "Operations"
            )
            UPDATE "Operations" AS operation
            SET "OperationNumber" = numbered.number
            FROM numbered
            WHERE operation."Id" = numbered."Id";

            SELECT setval(
                '"OperationNumberSequence"',
                COALESCE(MAX("OperationNumber"), 1),
                COUNT(*) > 0)
            FROM "Operations";
            """);

        migrationBuilder.CreateIndex(
            name: "IX_Operations_OperationNumber",
            table: "Operations",
            column: "OperationNumber",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Operations_OperationNumber",
            table: "Operations");

        migrationBuilder.DropColumn(
            name: "OperationNumber",
            table: "Operations");

        migrationBuilder.DropSequence(
            name: "OperationNumberSequence");
    }
}
