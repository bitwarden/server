using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.PostgresMigrations.Migrations;

/// <inheritdoc />
public partial class AddOrganizationIntegrationConfigurationDisabledState : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(
            name: "DisabledDate",
            table: "OrganizationIntegrationConfiguration",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "DisabledReason",
            table: "OrganizationIntegrationConfiguration",
            type: "integer",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "DisabledDate",
            table: "OrganizationIntegrationConfiguration");

        migrationBuilder.DropColumn(
            name: "DisabledReason",
            table: "OrganizationIntegrationConfiguration");
    }
}
