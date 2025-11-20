using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.MySqlMigrations.Migrations;

/// <inheritdoc />
public partial class _20251119_00_UpdateMemberAccessQuery : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<short>(
            name: "Status",
            table: "OrganizationMemberBaseDetails",
            type: "smallint",
            nullable: false,
            defaultValue: (short)0);

        migrationBuilder.AddColumn<Guid>(
            name: "UserId",
            table: "OrganizationMemberBaseDetails",
            type: "char(36)",
            nullable: true,
            collation: "ascii_general_ci");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "Status",
            table: "OrganizationMemberBaseDetails");

        migrationBuilder.DropColumn(
            name: "UserId",
            table: "OrganizationMemberBaseDetails");
    }
}
