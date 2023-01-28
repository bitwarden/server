using Microsoft.EntityFrameworkCore.Migrations;

namespace Bit.PostgresMigrations.Migrations;

public partial class FailedLoginCaptcha : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "FailedLoginCount",
            table: "User",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<DateTime>(
            name: "LastFailedLoginDate",
            table: "User",
            type: "timestamp without time zone",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "FailedLoginCount",
            table: "User");

        migrationBuilder.DropColumn(
            name: "LastFailedLoginDate",
            table: "User");
    }
}
