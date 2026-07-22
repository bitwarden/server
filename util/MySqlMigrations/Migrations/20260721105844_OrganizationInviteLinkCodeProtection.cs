using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.MySqlMigrations.Migrations;

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
            type: "longtext",
            nullable: false,
            oldClrType: typeof(Guid),
            oldType: "char(36)")
            .Annotation("MySql:CharSet", "utf8mb4")
            .OldAnnotation("Relational:Collation", "ascii_general_ci");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<Guid>(
            name: "Code",
            table: "OrganizationInviteLink",
            type: "char(36)",
            nullable: false,
            collation: "ascii_general_ci",
            oldClrType: typeof(string),
            oldType: "longtext")
            .OldAnnotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateIndex(
            name: "IX_OrganizationInviteLink_Code",
            table: "OrganizationInviteLink",
            column: "Code",
            unique: true);
    }
}
