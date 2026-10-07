using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.MySqlMigrations.Migrations;

/// <inheritdoc />
public partial class _20251028_00_AddOrganizationReportMetricColumns : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "ApplicationAtRiskCount",
            table: "OrganizationReport",
            type: "int",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "ApplicationCount",
            table: "OrganizationReport",
            type: "int",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "CriticalApplicationAtRiskCount",
            table: "OrganizationReport",
            type: "int",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "CriticalApplicationCount",
            table: "OrganizationReport",
            type: "int",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "CriticalMemberAtRiskCount",
            table: "OrganizationReport",
            type: "int",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "CriticalMemberCount",
            table: "OrganizationReport",
            type: "int",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "CriticalPasswordAtRiskCount",
            table: "OrganizationReport",
            type: "int",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "CriticalPasswordCount",
            table: "OrganizationReport",
            type: "int",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "MemberAtRiskCount",
            table: "OrganizationReport",
            type: "int",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "MemberCount",
            table: "OrganizationReport",
            type: "int",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "PasswordAtRiskCount",
            table: "OrganizationReport",
            type: "int",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "PasswordCount",
            table: "OrganizationReport",
            type: "int",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "ApplicationAtRiskCount",
            table: "OrganizationReport");

        migrationBuilder.DropColumn(
            name: "ApplicationCount",
            table: "OrganizationReport");

        migrationBuilder.DropColumn(
            name: "CriticalApplicationAtRiskCount",
            table: "OrganizationReport");

        migrationBuilder.DropColumn(
            name: "CriticalApplicationCount",
            table: "OrganizationReport");

        migrationBuilder.DropColumn(
            name: "CriticalMemberAtRiskCount",
            table: "OrganizationReport");

        migrationBuilder.DropColumn(
            name: "CriticalMemberCount",
            table: "OrganizationReport");

        migrationBuilder.DropColumn(
            name: "CriticalPasswordAtRiskCount",
            table: "OrganizationReport");

        migrationBuilder.DropColumn(
            name: "CriticalPasswordCount",
            table: "OrganizationReport");

        migrationBuilder.DropColumn(
            name: "MemberAtRiskCount",
            table: "OrganizationReport");

        migrationBuilder.DropColumn(
            name: "MemberCount",
            table: "OrganizationReport");

        migrationBuilder.DropColumn(
            name: "PasswordAtRiskCount",
            table: "OrganizationReport");

        migrationBuilder.DropColumn(
            name: "PasswordCount",
            table: "OrganizationReport");
    }
}
