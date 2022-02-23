using System;
using Bit.Core.Utilities;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Bit.PostgresMigrations.Migrations
{
    public partial class AddOrgApiKeyTable : Migration
    {
        private const string _scriptLocation = "PostgresMigrations.Scripts.2022-01-18_00_{0}_MigrateOrganizationApiKeys";

        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

            migrationBuilder.Sql(CoreHelpers.GetEmbeddedResourceContentsAsync(string.Format(_scriptLocation, "Up")));

            migrationBuilder.DropColumn(
                name: "ApiKey",
                table: "Organization");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ApiKey",
                table: "Organization",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.Sql(CoreHelpers.GetEmbeddedResourceContentsAsync(string.Format(_scriptLocation, "Down")));

            migrationBuilder.DropTable(
                name: "OrganizationApiKey");
        }
    }
}
