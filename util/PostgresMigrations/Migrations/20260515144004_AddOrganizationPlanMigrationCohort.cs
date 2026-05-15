using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.PostgresMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizationPlanMigrationCohort : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OrganizationPlanMigrationCohort",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    MigrationPathId = table.Column<byte>(type: "smallint", nullable: true),
                    ProactiveDiscountCouponCode = table.Column<string>(type: "text", nullable: true),
                    ChurnDiscountCouponCode = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RevisionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationPlanMigrationCohort", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OrganizationPlanMigrationCohortAssignment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CohortId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScheduledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    MigratedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ChurnDiscountAppliedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RevisionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationPlanMigrationCohortAssignment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrganizationPlanMigrationCohortAssignment_OrganizationPlanM~",
                        column: x => x.CohortId,
                        principalTable: "OrganizationPlanMigrationCohort",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OrganizationPlanMigrationCohortAssignment_Organization_Orga~",
                        column: x => x.OrganizationId,
                        principalTable: "Organization",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationPlanMigrationCohort_Name",
                table: "OrganizationPlanMigrationCohort",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationPlanMigrationCohortAssignment_CohortId_Schedule~",
                table: "OrganizationPlanMigrationCohortAssignment",
                columns: new[] { "CohortId", "ScheduledAt", "MigratedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationPlanMigrationCohortAssignment_OrganizationId",
                table: "OrganizationPlanMigrationCohortAssignment",
                column: "OrganizationId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrganizationPlanMigrationCohortAssignment");

            migrationBuilder.DropTable(
                name: "OrganizationPlanMigrationCohort");
        }
    }
}
