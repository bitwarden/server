using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.MySqlMigrations.Migrations;

/// <inheritdoc />
public partial class _2025_07_30_00_AlterOrganizationReport : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.RenameColumn(
            name: "Date",
            table: "OrganizationReport",
            newName: "RevisionDate");

        migrationBuilder.AddColumn<string>(
            name: "ApplicationData",
            table: "OrganizationReport",
            type: "longtext",
            nullable: false)
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.AddColumn<string>(
            name: "SummaryData",
            table: "OrganizationReport",
            type: "longtext",
            nullable: false)
            .Annotation("MySql:CharSet", "utf8mb4");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "ApplicationData",
            table: "OrganizationReport");

        migrationBuilder.DropColumn(
            name: "SummaryData",
            table: "OrganizationReport");

        migrationBuilder.RenameColumn(
            name: "RevisionDate",
            table: "OrganizationReport",
            newName: "Date");
    }
}
