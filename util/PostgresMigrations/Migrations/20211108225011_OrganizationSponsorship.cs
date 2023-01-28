using Microsoft.EntityFrameworkCore.Migrations;

namespace Bit.PostgresMigrations.Migrations;

public partial class OrganizationSponsorship : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "UsesCryptoAgent",
            table: "User",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.CreateTable(
            name: "OrganizationSponsorship",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                InstallationId = table.Column<Guid>(type: "uuid", nullable: true),
                SponsoringOrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                SponsoringOrganizationUserId = table.Column<Guid>(type: "uuid", nullable: true),
                SponsoredOrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                FriendlyName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                OfferedToEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                PlanSponsorshipType = table.Column<byte>(type: "smallint", nullable: true),
                CloudSponsor = table.Column<bool>(type: "boolean", nullable: false),
                LastSyncDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                TimesRenewedWithoutValidation = table.Column<byte>(type: "smallint", nullable: false),
                SponsorshipLapsedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OrganizationSponsorship", x => x.Id);
                table.ForeignKey(
                    name: "FK_OrganizationSponsorship_Installation_InstallationId",
                    column: x => x.InstallationId,
                    principalTable: "Installation",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_OrganizationSponsorship_Organization_SponsoredOrganizationId",
                    column: x => x.SponsoredOrganizationId,
                    principalTable: "Organization",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_OrganizationSponsorship_Organization_SponsoringOrganization~",
                    column: x => x.SponsoringOrganizationId,
                    principalTable: "Organization",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_OrganizationSponsorship_InstallationId",
            table: "OrganizationSponsorship",
            column: "InstallationId");

        migrationBuilder.CreateIndex(
            name: "IX_OrganizationSponsorship_SponsoredOrganizationId",
            table: "OrganizationSponsorship",
            column: "SponsoredOrganizationId");

        migrationBuilder.CreateIndex(
            name: "IX_OrganizationSponsorship_SponsoringOrganizationId",
            table: "OrganizationSponsorship",
            column: "SponsoringOrganizationId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "OrganizationSponsorship");

        migrationBuilder.DropColumn(
            name: "UsesCryptoAgent",
            table: "User");
    }
}
