using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.SqliteMigrations.Migrations;

/// <inheritdoc />
public partial class _20251119_00_UpdateMemberAccessQuery : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<Guid>(
            name: "CipherId",
            table: "OrganizationMemberBaseDetails",
            type: "TEXT",
            nullable: true,
            oldClrType: typeof(Guid),
            oldType: "TEXT");

        migrationBuilder.AddColumn<string>(
            name: "AvatarColor",
            table: "OrganizationMemberBaseDetails",
            type: "TEXT",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "AvatarColor",
            table: "OrganizationMemberBaseDetails");

        migrationBuilder.AlterColumn<Guid>(
            name: "CipherId",
            table: "OrganizationMemberBaseDetails",
            type: "TEXT",
            nullable: false,
            defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
            oldClrType: typeof(Guid),
            oldType: "TEXT",
            oldNullable: true);
    }
}
