using Microsoft.EntityFrameworkCore.Migrations;

namespace Bit.MySqlMigrations.Migrations;

public partial class SponsorshipBulkActions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_OrganizationSponsorship_Organization_SponsoringOrganizationId",
            table: "OrganizationSponsorship");

        migrationBuilder.AlterColumn<Guid>(
            name: "SponsoringOrganizationUserId",
            table: "OrganizationSponsorship",
            type: "char(36)",
            nullable: false,
            defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
            collation: "ascii_general_ci",
            oldClrType: typeof(Guid),
            oldType: "char(36)",
            oldNullable: true)
            .OldAnnotation("Relational:Collation", "ascii_general_ci");

        migrationBuilder.AlterColumn<Guid>(
            name: "SponsoringOrganizationId",
            table: "OrganizationSponsorship",
            type: "char(36)",
            nullable: false,
            defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
            collation: "ascii_general_ci",
            oldClrType: typeof(Guid),
            oldType: "char(36)",
            oldNullable: true)
            .OldAnnotation("Relational:Collation", "ascii_general_ci");

        migrationBuilder.AddForeignKey(
            name: "FK_OrganizationSponsorship_Organization_SponsoringOrganizationId",
            table: "OrganizationSponsorship",
            column: "SponsoringOrganizationId",
            principalTable: "Organization",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_OrganizationSponsorship_Organization_SponsoringOrganizationId",
            table: "OrganizationSponsorship");

        migrationBuilder.AlterColumn<Guid>(
            name: "SponsoringOrganizationUserId",
            table: "OrganizationSponsorship",
            type: "char(36)",
            nullable: true,
            collation: "ascii_general_ci",
            oldClrType: typeof(Guid),
            oldType: "char(36)")
            .OldAnnotation("Relational:Collation", "ascii_general_ci");

        migrationBuilder.AlterColumn<Guid>(
            name: "SponsoringOrganizationId",
            table: "OrganizationSponsorship",
            type: "char(36)",
            nullable: true,
            collation: "ascii_general_ci",
            oldClrType: typeof(Guid),
            oldType: "char(36)")
            .OldAnnotation("Relational:Collation", "ascii_general_ci");

        migrationBuilder.AddForeignKey(
            name: "FK_OrganizationSponsorship_Organization_SponsoringOrganizationId",
            table: "OrganizationSponsorship",
            column: "SponsoringOrganizationId",
            principalTable: "Organization",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }
}
