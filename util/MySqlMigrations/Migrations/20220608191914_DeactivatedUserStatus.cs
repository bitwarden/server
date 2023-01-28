using Microsoft.EntityFrameworkCore.Migrations;

namespace Bit.MySqlMigrations.Migrations;

public partial class DeactivatedUserStatus : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<short>(
            name: "Status",
            table: "OrganizationUser",
            type: "smallint",
            nullable: false,
            oldClrType: typeof(byte),
            oldType: "tinyint unsigned");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<byte>(
            name: "Status",
            table: "OrganizationUser",
            type: "tinyint unsigned",
            nullable: false,
            oldClrType: typeof(short),
            oldType: "smallint");
    }
}
