using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.MySqlMigrations.Migrations;

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
                Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                OrganizationId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                Type = table.Column<int>(type: "int", nullable: false),
                Configuration = table.Column<string>(type: "longtext", nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                CreationDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                RevisionDate = table.Column<DateTime>(type: "datetime(6)", nullable: false)
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
            })
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateTable(
            name: "OrganizationIntegrationConfiguration",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                OrganizationIntegrationId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                EventType = table.Column<int>(type: "int", nullable: false),
                Configuration = table.Column<string>(type: "longtext", nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                Template = table.Column<string>(type: "longtext", nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                CreationDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                RevisionDate = table.Column<DateTime>(type: "datetime(6)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OrganizationIntegrationConfiguration", x => x.Id);
                table.ForeignKey(
                    name: "FK_OrganizationIntegrationConfiguration_OrganizationIntegration~",
                    column: x => x.OrganizationIntegrationId,
                    principalTable: "OrganizationIntegration",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            })
            .Annotation("MySql:CharSet", "utf8mb4");

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
            name: "IX_OrganizationIntegrationConfiguration_OrganizationIntegration~",
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
