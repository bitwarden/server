using Microsoft.EntityFrameworkCore.Migrations;

namespace Bit.PostgresMigrations.Migrations;

public partial class SponsorshipBulkActions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_OrganizationSponsorship_Organization_SponsoringOrganization~",
            table: "OrganizationSponsorship");

        migrationBuilder.AlterColumn<Guid>(
            name: "SponsoringOrganizationUserId",
            table: "OrganizationSponsorship",
            type: "uuid",
            nullable: false,
            defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
            oldClrType: typeof(Guid),
            oldType: "uuid",
            oldNullable: true);

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

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_OrganizationSponsorship_Organization_SponsoringOrganization~",
            table: "OrganizationSponsorship");

        migrationBuilder.AlterColumn<Guid>(
            name: "SponsoringOrganizationUserId",
            table: "OrganizationSponsorship",
            type: "uuid",
            nullable: true,
            oldClrType: typeof(Guid),
            oldType: "uuid");

        migrationBuilder.AlterColumn<Guid>(
            name: "SponsoringOrganizationId",
            table: "OrganizationSponsorship",
            type: "uuid",
            nullable: true,
            oldClrType: typeof(Guid),
            oldType: "uuid");

        migrationBuilder.AddForeignKey(
            name: "FK_OrganizationSponsorship_Organization_SponsoringOrganization~",
            table: "OrganizationSponsorship",
            column: "SponsoringOrganizationId",
            principalTable: "Organization",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }
}
