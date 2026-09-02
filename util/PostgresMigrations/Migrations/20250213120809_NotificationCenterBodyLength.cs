using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.PostgresMigrations.Migrations;

/// <inheritdoc />
public partial class NotificationCenterBodyLength : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
            name: "Body",
            table: "Notification",
            type: "character varying(3000)",
            maxLength: 3000,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "text",
            oldNullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
            name: "Body",
            table: "Notification",
            type: "text",
            nullable: true,
            oldClrType: typeof(string),
            oldType: "character varying(3000)",
            oldMaxLength: 3000,
            oldNullable: true);
    }
}
