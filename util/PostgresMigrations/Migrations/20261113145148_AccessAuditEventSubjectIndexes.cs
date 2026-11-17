using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.PostgresMigrations.Migrations;

/// <inheritdoc />
public partial class AccessAuditEventSubjectIndexes : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "IX_AccessAuditEvent_OrganizationId_AccessRuleId_OccurredDate_Id",
            table: "AccessAuditEvent",
            columns: new[] { "OrganizationId", "AccessRuleId", "OccurredDate", "Id" },
            descending: new[] { false, false, true, true });

        migrationBuilder.CreateIndex(
            name: "IX_AccessAuditEvent_OrganizationId_CipherId_OccurredDate_Id",
            table: "AccessAuditEvent",
            columns: new[] { "OrganizationId", "CipherId", "OccurredDate", "Id" },
            descending: new[] { false, false, true, true });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_AccessAuditEvent_OrganizationId_AccessRuleId_OccurredDate_Id",
            table: "AccessAuditEvent");

        migrationBuilder.DropIndex(
            name: "IX_AccessAuditEvent_OrganizationId_CipherId_OccurredDate_Id",
            table: "AccessAuditEvent");
    }
}
