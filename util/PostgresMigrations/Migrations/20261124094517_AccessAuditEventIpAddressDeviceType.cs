using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.PostgresMigrations.Migrations;

/// <inheritdoc />
public partial class AccessAuditEventIpAddressDeviceType : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "IpAddress",
            table: "AccessAuditEvent",
            type: "character varying(50)",
            maxLength: 50,
            nullable: true);

        migrationBuilder.AddColumn<byte>(
            name: "DeviceType",
            table: "AccessAuditEvent",
            type: "smallint",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "IpAddress",
            table: "AccessAuditEvent");

        migrationBuilder.DropColumn(
            name: "DeviceType",
            table: "AccessAuditEvent");
    }
}
