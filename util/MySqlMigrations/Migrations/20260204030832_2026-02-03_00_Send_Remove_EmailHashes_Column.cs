using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.MySqlMigrations.Migrations;

/// <inheritdoc />
public partial class _20260203_00_Send_Remove_EmailHashes_Column : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.RenameColumn(
            name: "EmailHashes",
            table: "Send",
            newName: "AnonAccessEmails");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.RenameColumn(
            name: "AnonAccessEmails",
            table: "Send",
            newName: "EmailHashes");
    }
}
