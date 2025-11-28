using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.MySqlMigrations.Migrations;

/// <inheritdoc />
public partial class AddingIndexToEvents : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.RenameIndex(
            name: "IX_Event_Date_OrganizationId_ActingUserId_CipherId",
            table: "Event",
            newName: "IX_Event_DateOrganizationIdUserId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.RenameIndex(
            name: "IX_Event_DateOrganizationIdUserId",
            table: "Event",
            newName: "IX_Event_Date_OrganizationId_ActingUserId_CipherId");
    }
}
