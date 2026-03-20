using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.SqliteMigrations.Migrations;

/// <inheritdoc />
public partial class SendKeyAndDataNotNull : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
            name: "Key",
            table: "Send",
            type: "TEXT",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "TEXT",
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "Data",
            table: "Send",
            type: "TEXT",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "TEXT",
            oldNullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
            name: "Key",
            table: "Send",
            type: "TEXT",
            nullable: true,
            oldClrType: typeof(string),
            oldType: "TEXT");

        migrationBuilder.AlterColumn<string>(
            name: "Data",
            table: "Send",
            type: "TEXT",
            nullable: true,
            oldClrType: typeof(string),
            oldType: "TEXT");
    }
}
