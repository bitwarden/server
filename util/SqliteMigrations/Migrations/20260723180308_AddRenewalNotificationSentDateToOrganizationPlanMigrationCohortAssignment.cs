using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.SqliteMigrations.Migrations;

/// <inheritdoc />
public partial class AddRenewalNotificationSentDateToOrganizationPlanMigrationCohortAssignment : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(
            name: "RenewalNotificationSentDate",
            table: "OrganizationPlanMigrationCohortAssignment",
            type: "TEXT",
            nullable: true);

        migrationBuilder.Sql(
            "UPDATE OrganizationPlanMigrationCohortAssignment SET RenewalNotificationSentDate = ScheduledDate WHERE ScheduledDate IS NOT NULL");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "RenewalNotificationSentDate",
            table: "OrganizationPlanMigrationCohortAssignment");
    }
}
