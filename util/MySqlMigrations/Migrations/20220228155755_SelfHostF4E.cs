using System;
using Bit.Core.Utilities;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Bit.MySqlMigrations.Migrations
{
    public partial class SelfHostF4E : Migration
    {
        private const string _scriptTemplate = "Scripts.2022-01-18_00_{0}_MigrateOrganizationApiKeys.sql";

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OrganizationApiKey",
                columns: table => new
                {
                    OrganizationId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Type = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                    ApiKey = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RevisionDate = table.Column<DateTime>(type: "datetime(6)", nullable: false)
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
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.Sql(CoreHelpers.GetEmbeddedResourceContentsAsync(string.Format(_scriptTemplate, "Up")));

            migrationBuilder.DropColumn(
                name: "ApiKey",
                table: "Organization");

            migrationBuilder.CreateTable(
                name: "OrganizationConnection",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Type = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Enabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Config = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
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
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "OrganizationSponsorship",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    InstallationId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    SponsoringOrganizationId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    SponsoringOrganizationUserId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    SponsoredOrganizationId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    FriendlyName = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OfferedToEmail = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PlanSponsorshipType = table.Column<byte>(type: "tinyint unsigned", nullable: true),
                    CloudSponsor = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    LastSyncDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    TimesRenewedWithoutValidation = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                    SponsorshipLapsedDate = table.Column<DateTime>(type: "datetime(6)", nullable: true)
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
                        name: "FK_OrganizationSponsorship_Organization_SponsoringOrganizationId",
                        column: x => x.SponsoringOrganizationId,
                        principalTable: "Organization",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

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
            migrationBuilder.AddColumn<string>(
                name: "ApiKey",
                table: "Organization",
                type: "varchar(30)",
                maxLength: 30,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.Sql(CoreHelpers.GetEmbeddedResourceContentsAsync(string.Format(_scriptTemplate, "Down")));

            migrationBuilder.DropTable(
                name: "OrganizationApiKey");

            migrationBuilder.DropTable(
                name: "OrganizationConnection");

            migrationBuilder.DropTable(
                name: "OrganizationSponsorship");

            
        }
    }
}
