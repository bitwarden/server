using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.MySqlMigrations.Migrations
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
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Name = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    MigrationPathId = table.Column<byte>(type: "tinyint unsigned", nullable: true),
                    ProactiveDiscountCouponCode = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ChurnDiscountCouponCode = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    RevisionDate = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationPlanMigrationCohort", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "OrganizationPlanMigrationCohortAssignment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    OrganizationId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CohortId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ScheduledDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    MigratedDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ChurnDiscountAppliedDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreationDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    RevisionDate = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationPlanMigrationCohortAssignment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrganizationPlanMigrationCohortAssignment_OrganizationPlanMi~",
                        column: x => x.CohortId,
                        principalTable: "OrganizationPlanMigrationCohort",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OrganizationPlanMigrationCohortAssignment_Organization_Organ~",
                        column: x => x.OrganizationId,
                        principalTable: "Organization",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationPlanMigrationCohort_Name",
                table: "OrganizationPlanMigrationCohort",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationPlanMigrationCohortAssignment_CohortId_Scheduled~",
                table: "OrganizationPlanMigrationCohortAssignment",
                columns: new[] { "CohortId", "ScheduledDate", "MigratedDate" });

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
