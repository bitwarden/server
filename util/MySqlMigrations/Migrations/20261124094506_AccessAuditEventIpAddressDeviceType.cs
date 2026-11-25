using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.MySqlMigrations.Migrations;

/// <inheritdoc />
public partial class AccessAuditEventIpAddressDeviceType : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "IpAddress",
            table: "AccessAuditEvent",
            type: "varchar(50)",
            maxLength: 50,
            nullable: true)
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.AddColumn<byte>(
            name: "DeviceType",
            table: "AccessAuditEvent",
            type: "tinyint unsigned",
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
