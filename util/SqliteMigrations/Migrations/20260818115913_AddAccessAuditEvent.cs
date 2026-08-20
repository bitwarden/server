using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.SqliteMigrations.Migrations;

/// <inheritdoc />
public partial class AddAccessAuditEvent : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "AccessAuditEvent",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                OrganizationId = table.Column<Guid>(type: "TEXT", nullable: false),
                CorrelationId = table.Column<Guid>(type: "TEXT", nullable: false),
                Kind = table.Column<byte>(type: "INTEGER", nullable: false),
                Phase = table.Column<byte>(type: "INTEGER", nullable: false),
                OccurredAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                ActorId = table.Column<Guid>(type: "TEXT", nullable: true),
                RequesterId = table.Column<Guid>(type: "TEXT", nullable: true),
                CollectionId = table.Column<Guid>(type: "TEXT", nullable: true),
                CipherId = table.Column<Guid>(type: "TEXT", nullable: true),
                AccessRequestId = table.Column<Guid>(type: "TEXT", nullable: true),
                AccessLeaseId = table.Column<Guid>(type: "TEXT", nullable: true),
                AccessRuleId = table.Column<Guid>(type: "TEXT", nullable: true),
                Detail = table.Column<string>(type: "TEXT", nullable: true),
                LeaseNotBefore = table.Column<DateTime>(type: "TEXT", nullable: true),
                LeaseNotAfter = table.Column<DateTime>(type: "TEXT", nullable: true),
                ActorName = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                ActorEmail = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                RequesterName = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                RequesterEmail = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                CipherName = table.Column<string>(type: "TEXT", nullable: true),
                CollectionName = table.Column<string>(type: "TEXT", nullable: true),
                RuleName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AccessAuditEvent", x => x.Id);
                table.ForeignKey(
                    name: "FK_AccessAuditEvent_Organization_OrganizationId",
                    column: x => x.OrganizationId,
                    principalTable: "Organization",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_AccessAuditEvent_OrganizationId_OccurredAt",
            table: "AccessAuditEvent",
            columns: new[] { "OrganizationId", "OccurredAt" },
            descending: new[] { false, true });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "AccessAuditEvent");
    }
}
