using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.PostgresMigrations.Migrations;

/// <inheritdoc />
public partial class SendAuthTypeAndEmailLength : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
            name: "Emails",
            table: "Send",
            type: "character varying(4000)",
            maxLength: 4000,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "character varying(1024)",
            oldMaxLength: 1024,
            oldNullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
            name: "Emails",
            table: "Send",
            type: "character varying(1024)",
            maxLength: 1024,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "character varying(4000)",
            oldMaxLength: 4000,
            oldNullable: true);
    }
}
