using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.MySqlMigrations.Migrations;

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
            type: "tinyint(1)",
            nullable: false,
            defaultValue: false,
            oldClrType: typeof(bool),
            oldType: "tinyint(1)");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<bool>(
            name: "AccessAll",
            table: "OrganizationUser",
            type: "tinyint(1)",
            nullable: false,
            oldClrType: typeof(bool),
            oldType: "tinyint(1)",
            oldDefaultValue: false);

        migrationBuilder.CreateIndex(
            name: "IX_OrganizationUser_UserId_OrganizationId_Status",
            table: "OrganizationUser",
            columns: new[] { "UserId", "OrganizationId", "Status" });
    }
}
