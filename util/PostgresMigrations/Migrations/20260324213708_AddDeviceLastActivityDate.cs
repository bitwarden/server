using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.PostgresMigrations.Migrations;

/// <inheritdoc />
public partial class AddDeviceLastActivityDate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(
            name: "LastActivityDate",
            table: "Device",
            type: "timestamp with time zone",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "LastActivityDate",
            table: "Device");
    }
}
