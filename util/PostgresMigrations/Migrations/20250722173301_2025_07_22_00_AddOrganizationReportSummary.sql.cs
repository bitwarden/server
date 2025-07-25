using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.PostgresMigrations.Migrations;

/// <inheritdoc />
public partial class _2025_07_22_00_AddOrganizationReportSummarysql : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "OrganizationReportSummaries",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OrganizationReportId = table.Column<Guid>(type: "uuid", nullable: false),
                SummaryDetails = table.Column<string>(type: "text", nullable: false),
                ContentEncryptionKey = table.Column<string>(type: "text", nullable: false),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OrganizationReportSummaries", x => x.Id);
                table.ForeignKey(
                    name: "FK_OrganizationReportSummaries_OrganizationReport_Organization~",
                    column: x => x.OrganizationReportId,
                    principalTable: "OrganizationReport",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_OrganizationReportSummaries_OrganizationReportId",
            table: "OrganizationReportSummaries",
            column: "OrganizationReportId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "OrganizationReportSummaries");
    }
}
