using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.PostgresMigrations.Migrations;

/// <inheritdoc />
public partial class AlterAuthRequest : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "City",
            table: "AuthRequest",
            type: "character varying(256)",
            maxLength: 256,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "CountryName",
            table: "AuthRequest",
            type: "character varying(256)",
            maxLength: 256,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Region",
            table: "AuthRequest",
            type: "character varying(50)",
            maxLength: 50,
            nullable: true);
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
