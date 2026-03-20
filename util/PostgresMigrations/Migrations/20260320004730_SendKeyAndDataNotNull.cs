using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.PostgresMigrations.Migrations;

/// <inheritdoc />
public partial class SendKeyAndDataNotNull : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
            name: "Key",
            table: "Send",
            type: "text",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "text",
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "Data",
            table: "Send",
            type: "text",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "text",
            oldNullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
            name: "Key",
            table: "Send",
            type: "text",
            nullable: true,
            oldClrType: typeof(string),
            oldType: "text");

        migrationBuilder.AlterColumn<string>(
            name: "Data",
            table: "Send",
            type: "text",
            nullable: true,
            oldClrType: typeof(string),
            oldType: "text");
    }
}
