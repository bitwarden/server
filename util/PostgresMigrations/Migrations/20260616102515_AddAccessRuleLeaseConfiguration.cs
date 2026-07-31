using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.PostgresMigrations.Migrations;

/// <inheritdoc />
public partial class AddAccessRuleLeaseConfiguration : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "AllowsExtensions",
            table: "AccessRule",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<int>(
            name: "DefaultLeaseDurationSeconds",
            table: "AccessRule",
            type: "integer",
            nullable: true);

        migrationBuilder.AddColumn<bool>(
            name: "Enabled",
            table: "AccessRule",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<int>(
            name: "MaxExtensionDurationSeconds",
            table: "AccessRule",
            type: "integer",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "MaxLeaseDurationSeconds",
            table: "AccessRule",
            type: "integer",
            nullable: true);

        migrationBuilder.AddColumn<bool>(
            name: "SingleActiveLease",
            table: "AccessRule",
            type: "boolean",
            nullable: false,
            defaultValue: false);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "AllowsExtensions",
            table: "AccessRule");

        migrationBuilder.DropColumn(
            name: "DefaultLeaseDurationSeconds",
            table: "AccessRule");

        migrationBuilder.DropColumn(
            name: "Enabled",
            table: "AccessRule");

        migrationBuilder.DropColumn(
            name: "MaxExtensionDurationSeconds",
            table: "AccessRule");

        migrationBuilder.DropColumn(
            name: "MaxLeaseDurationSeconds",
            table: "AccessRule");

        migrationBuilder.DropColumn(
            name: "SingleActiveLease",
            table: "AccessRule");
    }
}
