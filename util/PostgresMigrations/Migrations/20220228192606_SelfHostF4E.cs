using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Bit.PostgresMigrations.Migrations
{
    public partial class SelfHostF4E : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApiKey",
                table: "Organization");

            migrationBuilder.CreateTable(
                name: "OrganizationApiKey",
                columns: table => new
                {
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<byte>(type: "smallint", nullable: false),
                    ApiKey = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    RevisionDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationApiKey", x => new { x.OrganizationId, x.Type });
                    table.ForeignKey(
                        name: "FK_OrganizationApiKey_Organization_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organization",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrganizationConnection",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<byte>(type: "smallint", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    Config = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationConnection", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrganizationConnection_Organization_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organization",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

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
                name: "IX_OrganizationConnection_OrganizationId",
                table: "OrganizationConnection",
                column: "OrganizationId");

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
                name: "OrganizationApiKey");

            migrationBuilder.DropTable(
                name: "OrganizationConnection");

            migrationBuilder.DropTable(
                name: "OrganizationSponsorship");

            migrationBuilder.AddColumn<string>(
                name: "ApiKey",
                table: "Organization",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);
        }
    }
}
