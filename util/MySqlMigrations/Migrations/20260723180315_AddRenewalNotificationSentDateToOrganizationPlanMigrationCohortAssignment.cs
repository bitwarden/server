using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.MySqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddRenewalNotificationSentDateToOrganizationPlanMigrationCohortAssignment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "RenewalNotificationSentDate",
                table: "OrganizationPlanMigrationCohortAssignment",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE `OrganizationPlanMigrationCohortAssignment` SET `RenewalNotificationSentDate` = `ScheduledDate` WHERE `ScheduledDate` IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RenewalNotificationSentDate",
                table: "OrganizationPlanMigrationCohortAssignment");
        }
    }
}
