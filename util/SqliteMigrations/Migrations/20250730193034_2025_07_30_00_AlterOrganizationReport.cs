using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.SqliteMigrations.Migrations;

/// <inheritdoc />
public partial class _2025_07_30_00_AlterOrganizationReport : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.RenameColumn(
            name: "Date",
            table: "OrganizationReport",
            newName: "SummaryData");

        migrationBuilder.AddColumn<string>(
            name: "ApplicationData",
            table: "OrganizationReport",
            type: "TEXT",
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<DateTime>(
            name: "RevisionDate",
            table: "OrganizationReport",
            type: "TEXT",
            nullable: false,
            defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "ApplicationData",
            table: "OrganizationReport");

        migrationBuilder.DropColumn(
            name: "RevisionDate",
            table: "OrganizationReport");

        migrationBuilder.RenameColumn(
            name: "SummaryData",
            table: "OrganizationReport",
            newName: "Date");
    }
}
