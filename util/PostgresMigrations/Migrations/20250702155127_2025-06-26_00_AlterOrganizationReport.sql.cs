using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.PostgresMigrations.Migrations;

/// <inheritdoc />
public partial class _20250626_00_AlterOrganizationReportsql : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "ContentEncryptionKey",
            table: "OrganizationReport",
            type: "text",
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "ContentEncryptionKey",
            table: "OrganizationApplication",
            type: "text",
            nullable: false,
            defaultValue: "");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "ContentEncryptionKey",
            table: "OrganizationReport");

        migrationBuilder.DropColumn(
            name: "ContentEncryptionKey",
            table: "OrganizationApplication");
    }
}
