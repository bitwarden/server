using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.SqliteMigrations.Migrations;

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
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                OrganizationId = table.Column<Guid>(type: "TEXT", nullable: false),
                Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                ApiKeyId = table.Column<Guid>(type: "TEXT", nullable: false),
                Status = table.Column<byte>(type: "INTEGER", nullable: false),
                LastHeartbeatDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                CreationDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                RevisionDate = table.Column<DateTime>(type: "TEXT", nullable: false)
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
            });

        migrationBuilder.CreateTable(
            name: "PamLeaseExpirySweep",
            columns: table => new
            {
                AccessLeaseId = table.Column<Guid>(type: "TEXT", nullable: false),
                SweptDate = table.Column<DateTime>(type: "TEXT", nullable: false)
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

        migrationBuilder.CreateTable(
            name: "PamTargetSystem",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                OrganizationId = table.Column<Guid>(type: "TEXT", nullable: false),
                Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                Method = table.Column<byte>(type: "INTEGER", nullable: false),
                SealedData = table.Column<string>(type: "TEXT", maxLength: 10000, nullable: true),
                Status = table.Column<byte>(type: "INTEGER", nullable: false),
                CreationDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                RevisionDate = table.Column<DateTime>(type: "TEXT", nullable: false)
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
            name: "PamAccessConnectorTargetAssignment",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                AccessConnectorId = table.Column<Guid>(type: "TEXT", nullable: false),
                TargetSystemId = table.Column<Guid>(type: "TEXT", nullable: false),
                OrganizationId = table.Column<Guid>(type: "TEXT", nullable: false),
                CreationDate = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PamAccessConnectorTargetAssignment", x => x.Id);
                table.ForeignKey(
                    name: "FK_PamAccessConnectorTargetAssignment_Organization_OrganizationId",
                    column: x => x.OrganizationId,
                    principalTable: "Organization",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_PamAccessConnectorTargetAssignment_PamAccessConnector_AccessConnectorId",
                    column: x => x.AccessConnectorId,
                    principalTable: "PamAccessConnector",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_PamAccessConnectorTargetAssignment_PamTargetSystem_TargetSystemId",
                    column: x => x.TargetSystemId,
                    principalTable: "PamTargetSystem",
                    principalColumn: "Id");
            });

        migrationBuilder.CreateTable(
            name: "PamRotationConfig",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                OrganizationId = table.Column<Guid>(type: "TEXT", nullable: false),
                CipherId = table.Column<Guid>(type: "TEXT", nullable: false),
                TargetSystemId = table.Column<Guid>(type: "TEXT", nullable: false),
                SealedData = table.Column<string>(type: "TEXT", maxLength: 10000, nullable: false),
                ScheduleCron = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                RotateOnAccessEnd = table.Column<bool>(type: "INTEGER", nullable: false),
                NextRotationDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                LastRotationDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                CreationDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                RevisionDate = table.Column<DateTime>(type: "TEXT", nullable: false)
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
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                RotationConfigId = table.Column<Guid>(type: "TEXT", nullable: false),
                Source = table.Column<byte>(type: "INTEGER", nullable: false),
                Action = table.Column<byte>(type: "INTEGER", nullable: false),
                ClaimedByAccessConnectorId = table.Column<Guid>(type: "TEXT", nullable: true),
                ClaimedDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                CreationDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                NextClaimableDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                ExpirationDate = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PamRotationJob", x => x.Id);
                table.ForeignKey(
                    name: "FK_PamRotationJob_PamRotationConfig_RotationConfigId",
                    column: x => x.RotationConfigId,
                    principalTable: "PamRotationConfig",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "PamRotationAttempt",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                JobId = table.Column<Guid>(type: "TEXT", nullable: false),
                ClaimedByAccessConnectorId = table.Column<Guid>(type: "TEXT", nullable: false),
                CipherUpdated = table.Column<bool>(type: "INTEGER", nullable: false),
                Action = table.Column<byte>(type: "INTEGER", nullable: false),
                FailureReason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                SyncState = table.Column<byte>(type: "INTEGER", nullable: true),
                SessionTermination = table.Column<byte>(type: "INTEGER", nullable: true),
                CreationDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                ResolvedDate = table.Column<DateTime>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PamRotationAttempt", x => x.Id);
                table.ForeignKey(
                    name: "FK_PamRotationAttempt_PamRotationJob_JobId",
                    column: x => x.JobId,
                    principalTable: "PamRotationJob",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "PamRotationJobTimeoutSweep",
            columns: table => new
            {
                RotationJobId = table.Column<Guid>(type: "TEXT", nullable: false),
                SweptDate = table.Column<DateTime>(type: "TEXT", nullable: false)
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
            });

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
            name: "IX_PamAccessConnectorTargetAssignment_AccessConnectorId_TargetSystemId",
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
            name: "IX_PamRotationConfig_NextRotationDate",
            table: "PamRotationConfig",
            column: "NextRotationDate");

        migrationBuilder.CreateIndex(
            name: "IX_PamRotationConfig_OrganizationId",
            table: "PamRotationConfig",
            column: "OrganizationId");

        migrationBuilder.CreateIndex(
            name: "IX_PamRotationConfig_TargetSystemId",
            table: "PamRotationConfig",
            column: "TargetSystemId");

        migrationBuilder.CreateIndex(
            name: "IX_PamRotationJob_Action_ExpirationDate",
            table: "PamRotationJob",
            columns: new[] { "Action", "ExpirationDate" });

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
