using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.PostgresMigrations.Migrations;

/// <inheritdoc />
public partial class OrganizationInviteLinkCodeProtection : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_OrganizationInviteLink_Code",
            table: "OrganizationInviteLink");

        migrationBuilder.AlterColumn<string>(
            name: "Code",
            table: "OrganizationInviteLink",
            type: "text",
            nullable: false,
            oldClrType: typeof(Guid),
            oldType: "uuid");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<Guid>(
            name: "Code",
            table: "OrganizationInviteLink",
            type: "uuid",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "text");

        migrationBuilder.CreateIndex(
            name: "IX_OrganizationInviteLink_Code",
            table: "OrganizationInviteLink",
            column: "Code",
            unique: true);
    }
}
