using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.SqliteMigrations.Migrations;

/// <inheritdoc />
public partial class OrganizationIntegrations : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "OrganizationIntegration",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                OrganizationId = table.Column<Guid>(type: "TEXT", nullable: false),
                Type = table.Column<int>(type: "INTEGER", nullable: false),
                Configuration = table.Column<string>(type: "TEXT", nullable: true),
                CreationDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                RevisionDate = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OrganizationIntegration", x => x.Id);
                table.ForeignKey(
                    name: "FK_OrganizationIntegration_Organization_OrganizationId",
                    column: x => x.OrganizationId,
                    principalTable: "Organization",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "OrganizationIntegrationConfiguration",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                OrganizationIntegrationId = table.Column<Guid>(type: "TEXT", nullable: false),
                EventType = table.Column<int>(type: "INTEGER", nullable: false),
                Configuration = table.Column<string>(type: "TEXT", nullable: true),
                Template = table.Column<string>(type: "TEXT", nullable: true),
                CreationDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                RevisionDate = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OrganizationIntegrationConfiguration", x => x.Id);
                table.ForeignKey(
                    name: "FK_OrganizationIntegrationConfiguration_OrganizationIntegration_OrganizationIntegrationId",
                    column: x => x.OrganizationIntegrationId,
                    principalTable: "OrganizationIntegration",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_OrganizationIntegration_OrganizationId",
            table: "OrganizationIntegration",
            column: "OrganizationId");

        migrationBuilder.CreateIndex(
            name: "IX_OrganizationIntegration_OrganizationId_Type",
            table: "OrganizationIntegration",
            columns: new[] { "OrganizationId", "Type" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_OrganizationIntegrationConfiguration_OrganizationIntegrationId",
            table: "OrganizationIntegrationConfiguration",
            column: "OrganizationIntegrationId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "OrganizationIntegrationConfiguration");

        migrationBuilder.DropTable(
            name: "OrganizationIntegration");
    }
}
