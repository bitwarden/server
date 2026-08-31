using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.MySqlMigrations.Migrations;

/// <inheritdoc />
public partial class PamRotationDaemon : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "DaemonId",
            table: "AccessAuditEvent",
            type: "char(36)",
            nullable: true,
            collation: "ascii_general_ci");

        migrationBuilder.AddColumn<string>(
            name: "DaemonName",
            table: "AccessAuditEvent",
            type: "varchar(200)",
            maxLength: 200,
            nullable: true)
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.AddColumn<Guid>(
            name: "RotationConfigId",
            table: "AccessAuditEvent",
            type: "char(36)",
            nullable: true,
            collation: "ascii_general_ci");

        migrationBuilder.AddColumn<Guid>(
            name: "RotationJobId",
            table: "AccessAuditEvent",
            type: "char(36)",
            nullable: true,
            collation: "ascii_general_ci");

        migrationBuilder.AddColumn<byte>(
            name: "RotationSource",
            table: "AccessAuditEvent",
            type: "tinyint unsigned",
            nullable: true);

        migrationBuilder.AddColumn<byte>(
            name: "SyncState",
            table: "AccessAuditEvent",
            type: "tinyint unsigned",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "TargetSystemId",
            table: "AccessAuditEvent",
            type: "char(36)",
            nullable: true,
            collation: "ascii_general_ci");

        migrationBuilder.AddColumn<string>(
            name: "TargetSystemName",
            table: "AccessAuditEvent",
            type: "varchar(200)",
            maxLength: 200,
            nullable: true)
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateTable(
            name: "PamDaemon",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                OrganizationId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                Name = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                ApiKeyId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                Status = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                LastHeartbeatAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                CreationDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                RevisionDate = table.Column<DateTime>(type: "datetime(6)", nullable: false)
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
            })
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateTable(
            name: "PamTargetSystem",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                OrganizationId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                Name = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                Method = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                Kind = table.Column<byte>(type: "tinyint unsigned", nullable: true),
                PasswordPolicy = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                SupportsSessionTermination = table.Column<bool>(type: "tinyint(1)", nullable: true),
                Status = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                CreationDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                RevisionDate = table.Column<DateTime>(type: "datetime(6)", nullable: false)
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
            })
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateTable(
            name: "PamDaemonTargetAssignment",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                DaemonId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                TargetSystemId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                OrganizationId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                CreationDate = table.Column<DateTime>(type: "datetime(6)", nullable: false)
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
            })
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateTable(
            name: "PamRotationConfig",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                OrganizationId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                CipherId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                TargetSystemId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                AccountIdentity = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                TerminateSessions = table.Column<bool>(type: "tinyint(1)", nullable: false),
                ScheduleCron = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                RotateOnAccessEnd = table.Column<bool>(type: "tinyint(1)", nullable: false),
                NextRotationAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                Enabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                LastRotationAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                CreationDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                RevisionDate = table.Column<DateTime>(type: "datetime(6)", nullable: false)
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
            })
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateTable(
            name: "PamRotationJob",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                RotationConfigId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                Source = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                Status = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                ClaimedByDaemonId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                ClaimedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                CreationDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                NextClaimableAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                ExpiresAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PamRotationJob", x => x.Id);
                table.ForeignKey(
                    name: "FK_PamRotationJob_PamRotationConfig_RotationConfigId",
                    column: x => x.RotationConfigId,
                    principalTable: "PamRotationConfig",
                    principalColumn: "Id");
            })
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateTable(
            name: "PamRotationAttempt",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                JobId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                ClaimedByDaemonId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                CipherUpdated = table.Column<bool>(type: "tinyint(1)", nullable: false),
                Status = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                FailureReason = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                SyncState = table.Column<byte>(type: "tinyint unsigned", nullable: true),
                SessionTermination = table.Column<byte>(type: "tinyint unsigned", nullable: true),
                CreationDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                ResolvedDate = table.Column<DateTime>(type: "datetime(6)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PamRotationAttempt", x => x.Id);
                table.ForeignKey(
                    name: "FK_PamRotationAttempt_PamRotationJob_JobId",
                    column: x => x.JobId,
                    principalTable: "PamRotationJob",
                    principalColumn: "Id");
            })
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateTable(
            name: "PamLeaseExpirySweep",
            columns: table => new
            {
                AccessLeaseId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                SweptDate = table.Column<DateTime>(type: "datetime(6)", nullable: false)
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
            })
            .Annotation("MySql:CharSet", "utf8mb4");

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
