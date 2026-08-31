using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.PostgresMigrations.Migrations;

/// <inheritdoc />
public partial class PamRotationDaemon : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "DaemonId",
            table: "AccessAuditEvent",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "DaemonName",
            table: "AccessAuditEvent",
            type: "character varying(200)",
            maxLength: 200,
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "RotationConfigId",
            table: "AccessAuditEvent",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "RotationJobId",
            table: "AccessAuditEvent",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<byte>(
            name: "RotationSource",
            table: "AccessAuditEvent",
            type: "smallint",
            nullable: true);

        migrationBuilder.AddColumn<byte>(
            name: "SyncState",
            table: "AccessAuditEvent",
            type: "smallint",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "TargetSystemId",
            table: "AccessAuditEvent",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "TargetSystemName",
            table: "AccessAuditEvent",
            type: "character varying(200)",
            maxLength: 200,
            nullable: true);

        migrationBuilder.CreateTable(
            name: "PamDaemon",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                ApiKeyId = table.Column<Guid>(type: "uuid", nullable: false),
                Status = table.Column<byte>(type: "smallint", nullable: false),
                LastHeartbeatAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                RevisionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PamDaemon", x => x.Id);
                table.ForeignKey(
                    name: "FK_PamDaemon_ApiKey_ApiKeyId",
                    column: x => x.ApiKeyId,
                    principalTable: "ApiKey",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_PamDaemon_Organization_OrganizationId",
                    column: x => x.OrganizationId,
                    principalTable: "Organization",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "PamTargetSystem",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Method = table.Column<byte>(type: "smallint", nullable: false),
                Kind = table.Column<byte>(type: "smallint", nullable: true),
                PasswordPolicy = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                SupportsSessionTermination = table.Column<bool>(type: "boolean", nullable: true),
                Status = table.Column<byte>(type: "smallint", nullable: false),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                RevisionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PamTargetSystem", x => x.Id);
                table.ForeignKey(
                    name: "FK_PamTargetSystem_Organization_OrganizationId",
                    column: x => x.OrganizationId,
                    principalTable: "Organization",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "PamDaemonTargetAssignment",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                DaemonId = table.Column<Guid>(type: "uuid", nullable: false),
                TargetSystemId = table.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PamDaemonTargetAssignment", x => x.Id);
                table.ForeignKey(
                    name: "FK_PamDaemonTargetAssignment_Organization_OrganizationId",
                    column: x => x.OrganizationId,
                    principalTable: "Organization",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_PamDaemonTargetAssignment_PamDaemon_DaemonId",
                    column: x => x.DaemonId,
                    principalTable: "PamDaemon",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_PamDaemonTargetAssignment_PamTargetSystem_TargetSystemId",
                    column: x => x.TargetSystemId,
                    principalTable: "PamTargetSystem",
                    principalColumn: "Id");
            });

        migrationBuilder.CreateTable(
            name: "PamRotationConfig",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                CipherId = table.Column<Guid>(type: "uuid", nullable: false),
                TargetSystemId = table.Column<Guid>(type: "uuid", nullable: false),
                AccountIdentity = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                TerminateSessions = table.Column<bool>(type: "boolean", nullable: false),
                ScheduleCron = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                RotateOnAccessEnd = table.Column<bool>(type: "boolean", nullable: false),
                NextRotationAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                Enabled = table.Column<bool>(type: "boolean", nullable: false),
                LastRotationAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                RevisionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PamRotationConfig", x => x.Id);
                table.ForeignKey(
                    name: "FK_PamRotationConfig_Organization_OrganizationId",
                    column: x => x.OrganizationId,
                    principalTable: "Organization",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_PamRotationConfig_PamTargetSystem_TargetSystemId",
                    column: x => x.TargetSystemId,
                    principalTable: "PamTargetSystem",
                    principalColumn: "Id");
            });

        migrationBuilder.CreateTable(
            name: "PamRotationJob",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                RotationConfigId = table.Column<Guid>(type: "uuid", nullable: false),
                Source = table.Column<byte>(type: "smallint", nullable: false),
                Status = table.Column<byte>(type: "smallint", nullable: false),
                ClaimedByDaemonId = table.Column<Guid>(type: "uuid", nullable: true),
                ClaimedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                NextClaimableAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PamRotationJob", x => x.Id);
                table.ForeignKey(
                    name: "FK_PamRotationJob_PamRotationConfig_RotationConfigId",
                    column: x => x.RotationConfigId,
                    principalTable: "PamRotationConfig",
                    principalColumn: "Id");
            });

        migrationBuilder.CreateTable(
            name: "PamRotationAttempt",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                JobId = table.Column<Guid>(type: "uuid", nullable: false),
                ClaimedByDaemonId = table.Column<Guid>(type: "uuid", nullable: false),
                CipherUpdated = table.Column<bool>(type: "boolean", nullable: false),
                Status = table.Column<byte>(type: "smallint", nullable: false),
                FailureReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                SyncState = table.Column<byte>(type: "smallint", nullable: true),
                SessionTermination = table.Column<byte>(type: "smallint", nullable: true),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                ResolvedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PamRotationAttempt", x => x.Id);
                table.ForeignKey(
                    name: "FK_PamRotationAttempt_PamRotationJob_JobId",
                    column: x => x.JobId,
                    principalTable: "PamRotationJob",
                    principalColumn: "Id");
            });

        migrationBuilder.CreateTable(
            name: "PamLeaseExpirySweep",
            columns: table => new
            {
                AccessLeaseId = table.Column<Guid>(type: "uuid", nullable: false),
                SweptDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PamLeaseExpirySweep", x => x.AccessLeaseId);
                table.ForeignKey(
                    name: "FK_PamLeaseExpirySweep_AccessLease_AccessLeaseId",
                    column: x => x.AccessLeaseId,
                    principalTable: "AccessLease",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_PamDaemon_ApiKeyId",
            table: "PamDaemon",
            column: "ApiKeyId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_PamDaemon_OrganizationId",
            table: "PamDaemon",
            column: "OrganizationId");

        migrationBuilder.CreateIndex(
            name: "IX_PamDaemonTargetAssignment_DaemonId_TargetSystemId",
            table: "PamDaemonTargetAssignment",
            columns: new[] { "DaemonId", "TargetSystemId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_PamDaemonTargetAssignment_OrganizationId",
            table: "PamDaemonTargetAssignment",
            column: "OrganizationId");

        migrationBuilder.CreateIndex(
            name: "IX_PamDaemonTargetAssignment_TargetSystemId",
            table: "PamDaemonTargetAssignment",
            column: "TargetSystemId");

        migrationBuilder.CreateIndex(
            name: "IX_PamRotationAttempt_ClaimedByDaemonId_JobId",
            table: "PamRotationAttempt",
            columns: new[] { "ClaimedByDaemonId", "JobId" });

        migrationBuilder.CreateIndex(
            name: "IX_PamRotationAttempt_JobId_Status",
            table: "PamRotationAttempt",
            columns: new[] { "JobId", "Status" });

        migrationBuilder.CreateIndex(
            name: "IX_PamRotationConfig_CipherId",
            table: "PamRotationConfig",
            column: "CipherId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_PamRotationConfig_NextRotationAt",
            table: "PamRotationConfig",
            column: "NextRotationAt");

        migrationBuilder.CreateIndex(
            name: "IX_PamRotationConfig_OrganizationId",
            table: "PamRotationConfig",
            column: "OrganizationId");

        migrationBuilder.CreateIndex(
            name: "IX_PamRotationConfig_TargetSystemId",
            table: "PamRotationConfig",
            column: "TargetSystemId");

        migrationBuilder.CreateIndex(
            name: "IX_PamRotationJob_ClaimedByDaemonId_Status",
            table: "PamRotationJob",
            columns: new[] { "ClaimedByDaemonId", "Status" });

        migrationBuilder.CreateIndex(
            name: "IX_PamRotationJob_RotationConfigId_Status",
            table: "PamRotationJob",
            columns: new[] { "RotationConfigId", "Status" });

        migrationBuilder.CreateIndex(
            name: "IX_PamRotationJob_Status_ExpiresAt",
            table: "PamRotationJob",
            columns: new[] { "Status", "ExpiresAt" });

        migrationBuilder.CreateIndex(
            name: "IX_PamTargetSystem_OrganizationId",
            table: "PamTargetSystem",
            column: "OrganizationId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "PamDaemonTargetAssignment");

        migrationBuilder.DropTable(
            name: "PamLeaseExpirySweep");

        migrationBuilder.DropTable(
            name: "PamRotationAttempt");

        migrationBuilder.DropTable(
            name: "PamDaemon");

        migrationBuilder.DropTable(
            name: "PamRotationJob");

        migrationBuilder.DropTable(
            name: "PamRotationConfig");

        migrationBuilder.DropTable(
            name: "PamTargetSystem");

        migrationBuilder.DropColumn(
            name: "DaemonId",
            table: "AccessAuditEvent");

        migrationBuilder.DropColumn(
            name: "DaemonName",
            table: "AccessAuditEvent");

        migrationBuilder.DropColumn(
            name: "RotationConfigId",
            table: "AccessAuditEvent");

        migrationBuilder.DropColumn(
            name: "RotationJobId",
            table: "AccessAuditEvent");

        migrationBuilder.DropColumn(
            name: "RotationSource",
            table: "AccessAuditEvent");

        migrationBuilder.DropColumn(
            name: "SyncState",
            table: "AccessAuditEvent");

        migrationBuilder.DropColumn(
            name: "TargetSystemId",
            table: "AccessAuditEvent");

        migrationBuilder.DropColumn(
            name: "TargetSystemName",
            table: "AccessAuditEvent");
    }
}
