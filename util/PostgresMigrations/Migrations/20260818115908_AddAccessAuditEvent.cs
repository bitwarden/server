using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.PostgresMigrations.Migrations;

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
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                CorrelationId = table.Column<Guid>(type: "uuid", nullable: false),
                Kind = table.Column<byte>(type: "smallint", nullable: false),
                Phase = table.Column<byte>(type: "smallint", nullable: false),
                OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                ActorId = table.Column<Guid>(type: "uuid", nullable: true),
                RequesterId = table.Column<Guid>(type: "uuid", nullable: true),
                CollectionId = table.Column<Guid>(type: "uuid", nullable: true),
                CipherId = table.Column<Guid>(type: "uuid", nullable: true),
                AccessRequestId = table.Column<Guid>(type: "uuid", nullable: true),
                AccessLeaseId = table.Column<Guid>(type: "uuid", nullable: true),
                AccessRuleId = table.Column<Guid>(type: "uuid", nullable: true),
                Detail = table.Column<string>(type: "text", nullable: true),
                LeaseNotBefore = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                LeaseNotAfter = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                ActorName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                ActorEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                RequesterName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                RequesterEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                CipherName = table.Column<string>(type: "text", nullable: true),
                CollectionName = table.Column<string>(type: "text", nullable: true),
                RuleName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
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
