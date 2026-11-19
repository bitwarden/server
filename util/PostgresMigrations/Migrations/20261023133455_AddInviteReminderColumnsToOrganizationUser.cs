using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.PostgresMigrations.Migrations;

/// <inheritdoc />
public partial class AddInviteReminderColumnsToOrganizationUser : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(
            name: "InviteReminderDueDate",
            table: "OrganizationUser",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<byte>(
            name: "InviteRemindersSentCount",
            table: "OrganizationUser",
            type: "smallint",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "InviteReminderDueDate",
            table: "OrganizationUser");

        migrationBuilder.DropColumn(
            name: "InviteRemindersSentCount",
            table: "OrganizationUser");
    }
}
