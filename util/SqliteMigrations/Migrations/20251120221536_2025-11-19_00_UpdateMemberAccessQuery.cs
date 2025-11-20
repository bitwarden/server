using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.SqliteMigrations.Migrations;

/// <inheritdoc />
public partial class _20251119_00_UpdateMemberAccessQuery : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<short>(
            name: "Status",
            table: "OrganizationMemberBaseDetails",
            type: "INTEGER",
            nullable: false,
            defaultValue: (short)0);

        migrationBuilder.AddColumn<Guid>(
            name: "UserId",
            table: "OrganizationMemberBaseDetails",
            type: "TEXT",
            nullable: true);
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
