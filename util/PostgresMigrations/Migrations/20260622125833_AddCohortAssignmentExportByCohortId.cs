using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.PostgresMigrations.Migrations;

/// <inheritdoc />
public partial class AddCohortAssignmentExportByCohortId : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "IX_OrganizationPlanMigrationCohortAssignment_CohortId_Creation~",
            table: "OrganizationPlanMigrationCohortAssignment",
            columns: new[] { "CohortId", "CreationDate", "Id" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_OrganizationPlanMigrationCohortAssignment_CohortId_Creation~",
            table: "OrganizationPlanMigrationCohortAssignment");
    }
}
