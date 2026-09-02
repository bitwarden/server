using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.PostgresMigrations.Migrations;

/// <inheritdoc />
public partial class OrganizationInviteLinkInviteBlob : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "EncryptedOrgKey",
            table: "OrganizationInviteLink");

        migrationBuilder.RenameColumn(
            name: "EncryptedInviteKey",
            table: "OrganizationInviteLink",
            newName: "Invite");

        migrationBuilder.AddColumn<bool>(
            name: "SupportsConfirmation",
            table: "OrganizationInviteLink",
            type: "boolean",
            nullable: false,
            defaultValue: false);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "SupportsConfirmation",
            table: "OrganizationInviteLink");

        migrationBuilder.RenameColumn(
            name: "Invite",
            table: "OrganizationInviteLink",
            newName: "EncryptedInviteKey");

        migrationBuilder.AddColumn<string>(
            name: "EncryptedOrgKey",
            table: "OrganizationInviteLink",
            type: "text",
            nullable: true);
    }
}
