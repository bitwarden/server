using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.PostgresMigrations.Migrations
{
    /// <inheritdoc />
    public partial class PageAccessAuditTrail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AccessAuditEvent_OrganizationId_OccurredAt",
                table: "AccessAuditEvent");

            migrationBuilder.CreateIndex(
                name: "IX_AccessAuditEvent_CorrelationId",
                table: "AccessAuditEvent",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_AccessAuditEvent_OrganizationId_OccurredAt_Id",
                table: "AccessAuditEvent",
                columns: new[] { "OrganizationId", "OccurredAt", "Id" },
                descending: new[] { false, true, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AccessAuditEvent_CorrelationId",
                table: "AccessAuditEvent");

            migrationBuilder.DropIndex(
                name: "IX_AccessAuditEvent_OrganizationId_OccurredAt_Id",
                table: "AccessAuditEvent");

            migrationBuilder.CreateIndex(
                name: "IX_AccessAuditEvent_OrganizationId_OccurredAt",
                table: "AccessAuditEvent",
                columns: new[] { "OrganizationId", "OccurredAt" },
                descending: new[] { false, true });
        }
    }
}
