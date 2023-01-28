using Microsoft.EntityFrameworkCore.Migrations;

namespace Bit.MySqlMigrations.Migrations;

public partial class OrganizationSponsorship : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "UsesCryptoAgent",
            table: "User",
            type: "tinyint(1)",
            nullable: false,
            defaultValue: false);

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
