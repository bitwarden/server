using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.MySqlMigrations.Migrations;

/// <inheritdoc />
public partial class _20260226_00_OrganizationReport_AddType_RemoveFileId : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "FileId",
            table: "OrganizationReport");

        migrationBuilder.AddColumn<byte>(
            name: "Type",
            table: "OrganizationReport",
            type: "tinyint unsigned",
            nullable: false,
            defaultValue: (byte)0);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "Type",
            table: "OrganizationReport");

        migrationBuilder.AddColumn<string>(
            name: "FileId",
            table: "OrganizationReport",
            type: "longtext",
            nullable: true)
            .Annotation("MySql:CharSet", "utf8mb4");
    }
}
