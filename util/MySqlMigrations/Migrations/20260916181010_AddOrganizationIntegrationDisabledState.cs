using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.MySqlMigrations.Migrations;

/// <inheritdoc />
public partial class AddOrganizationIntegrationDisabledState : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(
            name: "DisabledDate",
            table: "OrganizationIntegration",
            type: "datetime(6)",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "DisabledReason",
            table: "OrganizationIntegration",
            type: "int",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "DisabledDate",
            table: "OrganizationIntegration");

        migrationBuilder.DropColumn(
            name: "DisabledReason",
            table: "OrganizationIntegration");
    }
}
