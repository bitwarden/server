using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.PostgresMigrations.Migrations;

/// <inheritdoc />
public partial class OrganizationUserAccessAllDefaultValue : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_OrganizationUser_UserId_OrganizationId_Status",
            table: "OrganizationUser");

        migrationBuilder.AlterColumn<bool>(
            name: "AccessAll",
            table: "OrganizationUser",
            type: "boolean",
            nullable: false,
            defaultValue: false,
            oldClrType: typeof(bool),
            oldType: "boolean");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<bool>(
            name: "AccessAll",
            table: "OrganizationUser",
            type: "boolean",
            nullable: false,
            oldClrType: typeof(bool),
            oldType: "boolean",
            oldDefaultValue: false);

        migrationBuilder.CreateIndex(
            name: "IX_OrganizationUser_UserId_OrganizationId_Status",
            table: "OrganizationUser",
            columns: new[] { "UserId", "OrganizationId", "Status" })
            .Annotation("Npgsql:IndexInclude", new[] { "AccessAll" });
    }
}
