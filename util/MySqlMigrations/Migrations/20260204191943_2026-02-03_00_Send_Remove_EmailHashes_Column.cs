using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.MySqlMigrations.Migrations;

/// <inheritdoc />
public partial class _20260203_00_Send_Remove_EmailHashes_Column : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "EmailHashes",
            table: "Send");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "EmailHashes",
            table: "Send",
            type: "varchar(4000)",
            maxLength: 4000,
            nullable: true)
            .Annotation("MySql:CharSet", "utf8mb4");
    }
}
