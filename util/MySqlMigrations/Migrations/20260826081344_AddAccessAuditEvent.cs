using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.MySqlMigrations.Migrations;

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
                Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                OrganizationId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                CorrelationId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                Kind = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                Phase = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                OccurredDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                ActorId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                RequesterId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                CollectionId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                CipherId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                AccessRequestId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                AccessLeaseId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                AccessRuleId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                Detail = table.Column<string>(type: "longtext", nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                LeaseNotBefore = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                LeaseNotAfter = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                ActorName = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                ActorEmail = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                RequesterName = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                RequesterEmail = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                RuleName = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                TargetSystemId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                TargetSystemName = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                DaemonId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                DaemonName = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                RotationConfigId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                RotationJobId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                RotationSource = table.Column<byte>(type: "tinyint unsigned", nullable: true),
                SyncState = table.Column<byte>(type: "tinyint unsigned", nullable: true)
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
            })
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateIndex(
            name: "IX_AccessAuditEvent_OrganizationId_OccurredDate_Id",
            table: "AccessAuditEvent",
            columns: new[] { "OrganizationId", "OccurredDate", "Id" },
            descending: new[] { false, true, true });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "AccessAuditEvent");
    }
}
