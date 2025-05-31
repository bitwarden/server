using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.SqliteMigrations.Migrations;

/// <inheritdoc />
public partial class _2025051600_RiskInsightReportsql : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "RiskInsightCriticalApplication",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                OrganizationId = table.Column<Guid>(type: "TEXT", nullable: false),
                Applications = table.Column<string>(type: "TEXT", nullable: false),
                CreationDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                RevisionDate = table.Column<DateTime>(type: "TEXT", nullable: false)
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
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                OrganizationId = table.Column<Guid>(type: "TEXT", nullable: false),
                Date = table.Column<DateTime>(type: "TEXT", nullable: false),
                ReportData = table.Column<string>(type: "TEXT", nullable: false),
                CreationDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                RevisionDate = table.Column<DateTime>(type: "TEXT", nullable: false)
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

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "RiskInsightCriticalApplication");

        migrationBuilder.DropTable(
            name: "RiskInsightReport");
    }
}
