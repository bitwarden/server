using Bit.EfShared;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Bit.PostgresMigrations.Migrations;

public partial class SelfHostF4E : Migration
{
    private const string _scriptLocationTemplate = "2022-03-01_00_{0}_MigrateOrganizationApiKeys.psql";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_OrganizationSponsorship_Installation_InstallationId",
            table: "OrganizationSponsorship");

        migrationBuilder.DropIndex(
            name: "IX_OrganizationSponsorship_InstallationId",
            table: "OrganizationSponsorship");

        migrationBuilder.DropColumn(
            name: "InstallationId",
            table: "OrganizationSponsorship");

        migrationBuilder.DropColumn(
            name: "TimesRenewedWithoutValidation",
            table: "OrganizationSponsorship");

        migrationBuilder.CreateTable(
            name: "OrganizationApiKey",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                Type = table.Column<byte>(type: "smallint", nullable: false),
                ApiKey = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                RevisionDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OrganizationApiKey", x => x.Id);
                table.ForeignKey(
                    name: "FK_OrganizationApiKey_Organization_OrganizationId",
                    column: x => x.OrganizationId,
                    principalTable: "Organization",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.SqlResource(_scriptLocationTemplate);

        migrationBuilder.DropColumn(
            name: "ApiKey",
            table: "Organization");

        migrationBuilder.RenameColumn(
            name: "SponsorshipLapsedDate",
            table: "OrganizationSponsorship",
            newName: "ValidUntil");

        migrationBuilder.RenameColumn(
            name: "CloudSponsor",
            table: "OrganizationSponsorship",
            newName: "ToDelete");



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

        migrationBuilder.CreateIndex(
            name: "IX_OrganizationApiKey_OrganizationId",
            table: "OrganizationApiKey",
            column: "OrganizationId");

        migrationBuilder.CreateIndex(
            name: "IX_OrganizationConnection_OrganizationId",
            table: "OrganizationConnection",
            column: "OrganizationId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "ApiKey",
            table: "Organization",
            type: "character varying(30)",
            maxLength: 30,
            nullable: true);

        migrationBuilder.SqlResource(_scriptLocationTemplate);

        migrationBuilder.DropTable(
            name: "OrganizationApiKey");

        migrationBuilder.DropTable(
            name: "OrganizationConnection");

        migrationBuilder.RenameColumn(
            name: "ValidUntil",
            table: "OrganizationSponsorship",
            newName: "SponsorshipLapsedDate");

        migrationBuilder.RenameColumn(
            name: "ToDelete",
            table: "OrganizationSponsorship",
            newName: "CloudSponsor");

        migrationBuilder.AddColumn<Guid>(
            name: "InstallationId",
            table: "OrganizationSponsorship",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<byte>(
            name: "TimesRenewedWithoutValidation",
            table: "OrganizationSponsorship",
            type: "smallint",
            nullable: false,
            defaultValue: (byte)0);



        migrationBuilder.CreateIndex(
            name: "IX_OrganizationSponsorship_InstallationId",
            table: "OrganizationSponsorship",
            column: "InstallationId");

        migrationBuilder.AddForeignKey(
            name: "FK_OrganizationSponsorship_Installation_InstallationId",
            table: "OrganizationSponsorship",
            column: "InstallationId",
            principalTable: "Installation",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }
}
