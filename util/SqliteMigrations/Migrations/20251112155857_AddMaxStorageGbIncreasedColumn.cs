using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.SqliteMigrations.Migrations;

/// <inheritdoc />
public partial class AddMaxStorageGbIncreasedColumn : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<short>(
            name: "MaxStorageGbIncreased",
            table: "User",
            type: "INTEGER",
            nullable: true);

        migrationBuilder.AddColumn<short>(
            name: "MaxStorageGbIncreased",
            table: "Organization",
            type: "INTEGER",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "MaxStorageGbIncreased",
            table: "User");

        migrationBuilder.DropColumn(
            name: "MaxStorageGbIncreased",
            table: "Organization");
    }
}
