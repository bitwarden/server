using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.SqliteMigrations.Migrations;

/// <inheritdoc />
public partial class AddCipherExclusions : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Exclusions",
            table: "Cipher",
            type: "TEXT",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "Exclusions",
            table: "Cipher");
    }
}
