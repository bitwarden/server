using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.MySqlMigrations.Migrations;

/// <inheritdoc />
public partial class AddPamRotation : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "PamAccessConnector",
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
                table.PrimaryKey("PK_PamAccessConnector", x => x.Id);
                table.ForeignKey(
                    name: "FK_PamAccessConnector_ApiKey_ApiKeyId",
                    column: x => x.ApiKeyId,
                    principalTable: "ApiKey",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_PamAccessConnector_Organization_OrganizationId",
                    column: x => x.OrganizationId,
                    principalTable: "Organization",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
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

        migrationBuilder.CreateTable(
            name: "PamTargetSystem",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                OrganizationId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                Name = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                Method = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                SealedData = table.Column<string>(type: "varchar(10000)", maxLength: 10000, nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
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
            name: "PamAccessConnectorTargetAssignment",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                AccessConnectorId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                TargetSystemId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                OrganizationId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                CreationDate = table.Column<DateTime>(type: "datetime(6)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PamAccessConnectorTargetAssignment", x => x.Id);
                table.ForeignKey(
                    name: "FK_PamAccessConnectorTargetAssignment_Organization_Organization~",
                    column: x => x.OrganizationId,
                    principalTable: "Organization",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_PamAccessConnectorTargetAssignment_PamAccessConnector_Access~",
                    column: x => x.AccessConnectorId,
                    principalTable: "PamAccessConnector",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_PamAccessConnectorTargetAssignment_PamTargetSystem_TargetSys~",
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
                SealedData = table.Column<string>(type: "varchar(10000)", maxLength: 10000, nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
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
                Action = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                ClaimedByAccessConnectorId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
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
                ClaimedByAccessConnectorId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                CipherUpdated = table.Column<bool>(type: "tinyint(1)", nullable: false),
                Action = table.Column<byte>(type: "tinyint unsigned", nullable: false),
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
            name: "PamRotationJobTimeoutSweep",
            columns: table => new
            {
                RotationJobId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                SweptDate = table.Column<DateTime>(type: "datetime(6)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PamRotationJobTimeoutSweep", x => x.RotationJobId);
                table.ForeignKey(
                    name: "FK_PamRotationJobTimeoutSweep_PamRotationJob_RotationJobId",
                    column: x => x.RotationJobId,
                    principalTable: "PamRotationJob",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            })
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateIndex(
            name: "IX_PamAccessConnector_ApiKeyId",
            table: "PamAccessConnector",
            column: "ApiKeyId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_PamAccessConnector_OrganizationId",
            table: "PamAccessConnector",
            column: "OrganizationId");

        migrationBuilder.CreateIndex(
            name: "IX_PamAccessConnectorTargetAssignment_AccessConnectorId_TargetS~",
            table: "PamAccessConnectorTargetAssignment",
            columns: new[] { "AccessConnectorId", "TargetSystemId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_PamAccessConnectorTargetAssignment_OrganizationId",
            table: "PamAccessConnectorTargetAssignment",
            column: "OrganizationId");

        migrationBuilder.CreateIndex(
            name: "IX_PamAccessConnectorTargetAssignment_TargetSystemId",
            table: "PamAccessConnectorTargetAssignment",
            column: "TargetSystemId");

        migrationBuilder.CreateIndex(
            name: "IX_PamRotationAttempt_ClaimedByAccessConnectorId_JobId",
            table: "PamRotationAttempt",
            columns: new[] { "ClaimedByAccessConnectorId", "JobId" });

        migrationBuilder.CreateIndex(
            name: "IX_PamRotationAttempt_JobId_Action",
            table: "PamRotationAttempt",
            columns: new[] { "JobId", "Action" });

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
            name: "IX_PamRotationJob_Action_ExpiresAt",
            table: "PamRotationJob",
            columns: new[] { "Action", "ExpiresAt" });

        migrationBuilder.CreateIndex(
            name: "IX_PamRotationJob_ClaimedByAccessConnectorId_Action",
            table: "PamRotationJob",
            columns: new[] { "ClaimedByAccessConnectorId", "Action" });

        migrationBuilder.CreateIndex(
            name: "IX_PamRotationJob_RotationConfigId_Action",
            table: "PamRotationJob",
            columns: new[] { "RotationConfigId", "Action" });

        migrationBuilder.CreateIndex(
            name: "IX_PamTargetSystem_OrganizationId",
            table: "PamTargetSystem",
            column: "OrganizationId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "PamAccessConnectorTargetAssignment");

        migrationBuilder.DropTable(
            name: "PamLeaseExpirySweep");

        migrationBuilder.DropTable(
            name: "PamRotationAttempt");

        migrationBuilder.DropTable(
            name: "PamRotationJobTimeoutSweep");

        migrationBuilder.DropTable(
            name: "PamAccessConnector");

        migrationBuilder.DropTable(
            name: "PamRotationJob");

        migrationBuilder.DropTable(
            name: "PamRotationConfig");

        migrationBuilder.DropTable(
            name: "PamTargetSystem");
    }
}
