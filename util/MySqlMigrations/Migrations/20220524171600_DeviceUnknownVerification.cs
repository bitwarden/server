using Microsoft.EntityFrameworkCore.Migrations;

namespace Bit.MySqlMigrations.Migrations;

public partial class DeviceUnknownVerification : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "UnknownDeviceVerificationEnabled",
            table: "User",
            type: "tinyint(1)",
            nullable: false,
            defaultValue: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "UnknownDeviceVerificationEnabled",
            table: "User");
    }
}
