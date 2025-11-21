using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.MySqlMigrations.Migrations;

/// <inheritdoc />
public partial class UpdateMemberAccessQuery : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.RenameColumn(
            name: "UserGuid",
            table: "OrganizationMemberBaseDetails",
            newName: "OrganizationUserId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.RenameColumn(
            name: "OrganizationUserId",
            table: "OrganizationMemberBaseDetails",
            newName: "UserGuid");
    }
}
