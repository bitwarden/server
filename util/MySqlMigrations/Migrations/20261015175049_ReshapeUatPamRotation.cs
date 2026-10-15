using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.MySqlMigrations.Migrations;

/// <inheritdoc />
public partial class ReshapeUatPamRotation : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_PamRotationAttempt_PamRotationJob_JobId",
            table: "PamRotationAttempt");

        migrationBuilder.DropForeignKey(
            name: "FK_PamRotationJob_PamRotationConfig_RotationConfigId",
            table: "PamRotationJob");

        migrationBuilder.DropIndex(
            name: "IX_PamRotationAttempt_ClaimedByAccessConnectorId_JobId",
            table: "PamRotationAttempt");

        migrationBuilder.RenameColumn(
            name: "NextClaimableAt",
            table: "PamRotationJob",
            newName: "NextClaimableDate");

        migrationBuilder.RenameColumn(
            name: "ExpiresAt",
            table: "PamRotationJob",
            newName: "ExpirationDate");

        migrationBuilder.RenameColumn(
            name: "ClaimedAt",
            table: "PamRotationJob",
            newName: "ClaimedDate");

        migrationBuilder.RenameIndex(
            name: "IX_PamRotationJob_Action_ExpiresAt",
            table: "PamRotationJob",
            newName: "IX_PamRotationJob_Action_ExpirationDate");

        migrationBuilder.RenameColumn(
            name: "NextRotationAt",
            table: "PamRotationConfig",
            newName: "NextRotationDate");

        migrationBuilder.RenameColumn(
            name: "LastRotationAt",
            table: "PamRotationConfig",
            newName: "LastRotationDate");

        migrationBuilder.RenameIndex(
            name: "IX_PamRotationConfig_NextRotationAt",
            table: "PamRotationConfig",
            newName: "IX_PamRotationConfig_NextRotationDate");

        migrationBuilder.RenameColumn(
            name: "LastHeartbeatAt",
            table: "PamAccessConnector",
            newName: "LastHeartbeatDate");

        migrationBuilder.CreateIndex(
            name: "IX_PamRotationAttempt_ClaimedByAccessConnectorId_CreationDate",
            table: "PamRotationAttempt",
            columns: new[] { "ClaimedByAccessConnectorId", "CreationDate" },
            descending: new[] { false, true });

        migrationBuilder.AddForeignKey(
            name: "FK_PamRotationAttempt_PamRotationJob_JobId",
            table: "PamRotationAttempt",
            column: "JobId",
            principalTable: "PamRotationJob",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "FK_PamRotationJob_PamRotationConfig_RotationConfigId",
            table: "PamRotationJob",
            column: "RotationConfigId",
            principalTable: "PamRotationConfig",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_PamRotationAttempt_PamRotationJob_JobId",
            table: "PamRotationAttempt");

        migrationBuilder.DropForeignKey(
            name: "FK_PamRotationJob_PamRotationConfig_RotationConfigId",
            table: "PamRotationJob");

        migrationBuilder.DropIndex(
            name: "IX_PamRotationAttempt_ClaimedByAccessConnectorId_CreationDate",
            table: "PamRotationAttempt");

        migrationBuilder.RenameColumn(
            name: "NextClaimableDate",
            table: "PamRotationJob",
            newName: "NextClaimableAt");

        migrationBuilder.RenameColumn(
            name: "ExpirationDate",
            table: "PamRotationJob",
            newName: "ExpiresAt");

        migrationBuilder.RenameColumn(
            name: "ClaimedDate",
            table: "PamRotationJob",
            newName: "ClaimedAt");

        migrationBuilder.RenameIndex(
            name: "IX_PamRotationJob_Action_ExpirationDate",
            table: "PamRotationJob",
            newName: "IX_PamRotationJob_Action_ExpiresAt");

        migrationBuilder.RenameColumn(
            name: "NextRotationDate",
            table: "PamRotationConfig",
            newName: "NextRotationAt");

        migrationBuilder.RenameColumn(
            name: "LastRotationDate",
            table: "PamRotationConfig",
            newName: "LastRotationAt");

        migrationBuilder.RenameIndex(
            name: "IX_PamRotationConfig_NextRotationDate",
            table: "PamRotationConfig",
            newName: "IX_PamRotationConfig_NextRotationAt");

        migrationBuilder.RenameColumn(
            name: "LastHeartbeatDate",
            table: "PamAccessConnector",
            newName: "LastHeartbeatAt");

        migrationBuilder.CreateIndex(
            name: "IX_PamRotationAttempt_ClaimedByAccessConnectorId_JobId",
            table: "PamRotationAttempt",
            columns: new[] { "ClaimedByAccessConnectorId", "JobId" });

        migrationBuilder.AddForeignKey(
            name: "FK_PamRotationAttempt_PamRotationJob_JobId",
            table: "PamRotationAttempt",
            column: "JobId",
            principalTable: "PamRotationJob",
            principalColumn: "Id");

        migrationBuilder.AddForeignKey(
            name: "FK_PamRotationJob_PamRotationConfig_RotationConfigId",
            table: "PamRotationJob",
            column: "RotationConfigId",
            principalTable: "PamRotationConfig",
            principalColumn: "Id");
    }
}
