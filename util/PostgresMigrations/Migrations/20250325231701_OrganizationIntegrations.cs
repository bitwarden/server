using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.PostgresMigrations.Migrations;

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
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                Type = table.Column<int>(type: "integer", nullable: false),
                Configuration = table.Column<string>(type: "text", nullable: true),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                RevisionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OrganizationIntegrationId = table.Column<Guid>(type: "uuid", nullable: false),
                EventType = table.Column<int>(type: "integer", nullable: false),
                Configuration = table.Column<string>(type: "text", nullable: true),
                Template = table.Column<string>(type: "text", nullable: true),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                RevisionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OrganizationIntegrationConfiguration", x => x.Id);
                table.ForeignKey(
                    name: "FK_OrganizationIntegrationConfiguration_OrganizationIntegratio~",
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
            name: "IX_OrganizationIntegrationConfiguration_OrganizationIntegratio~",
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
