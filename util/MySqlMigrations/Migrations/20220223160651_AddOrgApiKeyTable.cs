using System;
using Bit.Core.Utilities;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Bit.MySqlMigrations.Migrations
{
    public partial class AddOrgApiKeyTable : Migration
    {
        private const string _scriptLocation = "MySqlMigrations.Scripts.2022-01-18_00_{0}_MigrateOrganizationApiKeys.sql";

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
                type: "varchar(30)",
                maxLength: 30,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
            
            migrationBuilder.Sql(CoreHelpers.GetEmbeddedResourceContentsAsync(string.Format(_scriptLocation, "Down")));

            migrationBuilder.DropTable(
                name: "OrganizationApiKey");
        }
    }
}
