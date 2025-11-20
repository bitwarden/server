using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.PostgresMigrations.Migrations;

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
            type: "uuid",
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
