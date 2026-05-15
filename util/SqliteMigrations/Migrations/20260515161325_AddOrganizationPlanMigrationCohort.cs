using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.SqliteMigrations.Migrations
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
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    MigrationPathId = table.Column<byte>(type: "INTEGER", nullable: true),
                    ProactiveDiscountCouponCode = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    ChurnDiscountCouponCode = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    RevisionDate = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationPlanMigrationCohort", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OrganizationPlanMigrationCohortAssignment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CohortId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ScheduledAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    MigratedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ChurnDiscountAppliedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    RevisionDate = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationPlanMigrationCohortAssignment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrganizationPlanMigrationCohortAssignment_OrganizationPlanMigrationCohort_CohortId",
                        column: x => x.CohortId,
                        principalTable: "OrganizationPlanMigrationCohort",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OrganizationPlanMigrationCohortAssignment_Organization_OrganizationId",
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
                name: "IX_OrganizationPlanMigrationCohortAssignment_CohortId_ScheduledAt_MigratedAt",
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
