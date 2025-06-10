using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.PostgresMigrations.Migrations;

/// <inheritdoc />
public partial class _2025061000_OrganizationReportsql : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "RiskInsightCriticalApplication");

        migrationBuilder.DropTable(
            name: "RiskInsightReport");

        migrationBuilder.CreateTable(
            name: "OrganizationApplication",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                Applications = table.Column<string>(type: "text", nullable: false),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                RevisionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OrganizationApplication", x => x.Id);
                table.ForeignKey(
                    name: "FK_OrganizationApplication_Organization_OrganizationId",
                    column: x => x.OrganizationId,
                    principalTable: "Organization",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "OrganizationReport",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                Date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                ReportData = table.Column<string>(type: "text", nullable: false),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                RevisionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OrganizationReport", x => x.Id);
                table.ForeignKey(
                    name: "FK_OrganizationReport_Organization_OrganizationId",
                    column: x => x.OrganizationId,
                    principalTable: "Organization",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_OrganizationApplication_Id",
            table: "OrganizationApplication",
            column: "Id");

        migrationBuilder.CreateIndex(
            name: "IX_OrganizationApplication_OrganizationId",
            table: "OrganizationApplication",
            column: "OrganizationId");

        migrationBuilder.CreateIndex(
            name: "IX_OrganizationReport_Id",
            table: "OrganizationReport",
            column: "Id");

        migrationBuilder.CreateIndex(
            name: "IX_OrganizationReport_OrganizationId",
            table: "OrganizationReport",
            column: "OrganizationId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "OrganizationApplication");

        migrationBuilder.DropTable(
            name: "OrganizationReport");

        migrationBuilder.CreateTable(
            name: "RiskInsightCriticalApplication",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                Applications = table.Column<string>(type: "text", nullable: false),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                RevisionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_RiskInsightCriticalApplication", x => x.Id);
                table.ForeignKey(
                    name: "FK_RiskInsightCriticalApplication_Organization_OrganizationId",
                    column: x => x.OrganizationId,
                    principalTable: "Organization",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "RiskInsightReport",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                Date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                ReportData = table.Column<string>(type: "text", nullable: false),
                RevisionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_RiskInsightReport", x => x.Id);
                table.ForeignKey(
                    name: "FK_RiskInsightReport_Organization_OrganizationId",
                    column: x => x.OrganizationId,
                    principalTable: "Organization",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_RiskInsightCriticalApplication_Id",
            table: "RiskInsightCriticalApplication",
            column: "Id");

        migrationBuilder.CreateIndex(
            name: "IX_RiskInsightCriticalApplication_OrganizationId",
            table: "RiskInsightCriticalApplication",
            column: "OrganizationId");

        migrationBuilder.CreateIndex(
            name: "IX_RiskInsightReport_Id",
            table: "RiskInsightReport",
            column: "Id");

        migrationBuilder.CreateIndex(
            name: "IX_RiskInsightReport_OrganizationId",
            table: "RiskInsightReport",
            column: "OrganizationId");
    }
}
