using Microsoft.EntityFrameworkCore.Migrations;

namespace Bit.PostgresMigrations.Migrations;

public partial class AddInstallationIdToEvents : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_OrganizationSponsorship_Organization_SponsoringOrganization~",
            table: "OrganizationSponsorship");

        migrationBuilder.AlterColumn<Guid>(
            name: "SponsoringOrganizationId",
            table: "OrganizationSponsorship",
            type: "uuid",
            nullable: true,
            oldClrType: typeof(Guid),
            oldType: "uuid");

        migrationBuilder.AddColumn<Guid>(
            name: "InstallationId",
            table: "Event",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddForeignKey(
            name: "FK_OrganizationSponsorship_Organization_SponsoringOrganization~",
            table: "OrganizationSponsorship",
            column: "SponsoringOrganizationId",
            principalTable: "Organization",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_OrganizationSponsorship_Organization_SponsoringOrganization~",
            table: "OrganizationSponsorship");

        migrationBuilder.DropColumn(
            name: "InstallationId",
            table: "Event");

        migrationBuilder.AlterColumn<Guid>(
            name: "SponsoringOrganizationId",
            table: "OrganizationSponsorship",
            type: "uuid",
            nullable: false,
            defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
            oldClrType: typeof(Guid),
            oldType: "uuid",
            oldNullable: true);

        migrationBuilder.AddForeignKey(
            name: "FK_OrganizationSponsorship_Organization_SponsoringOrganization~",
            table: "OrganizationSponsorship",
            column: "SponsoringOrganizationId",
            principalTable: "Organization",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);
    }
}
