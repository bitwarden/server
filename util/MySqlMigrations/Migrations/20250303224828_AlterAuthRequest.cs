using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.MySqlMigrations.Migrations;

/// <inheritdoc />
public partial class AlterAuthRequest : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "City",
            table: "AuthRequest",
            type: "varchar(256)",
            maxLength: 256,
            nullable: true)
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.AddColumn<string>(
            name: "CountryName",
            table: "AuthRequest",
            type: "varchar(256)",
            maxLength: 256,
            nullable: true)
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.AddColumn<string>(
            name: "Region",
            table: "AuthRequest",
            type: "varchar(50)",
            maxLength: 50,
            nullable: true)
            .Annotation("MySql:CharSet", "utf8mb4");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "City",
            table: "AuthRequest");

        migrationBuilder.DropColumn(
            name: "CountryName",
            table: "AuthRequest");

        migrationBuilder.DropColumn(
            name: "Region",
            table: "AuthRequest");
    }
}
