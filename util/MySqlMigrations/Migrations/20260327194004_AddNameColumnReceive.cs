using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.MySqlMigrations.Migrations;

/// <inheritdoc />
public partial class AddNameColumnReceive : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<DateTime>(
            name: "ExpirationDate",
            table: "Receive",
            type: "datetime(6)",
            nullable: false,
            defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
            oldClrType: typeof(DateTime),
            oldType: "datetime(6)",
            oldNullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Name",
            table: "Receive",
            type: "longtext",
            nullable: false)
            .Annotation("MySql:CharSet", "utf8mb4");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "Name",
            table: "Receive");

        migrationBuilder.AlterColumn<DateTime>(
            name: "ExpirationDate",
            table: "Receive",
            type: "datetime(6)",
            nullable: true,
            oldClrType: typeof(DateTime),
            oldType: "datetime(6)");
    }
}
