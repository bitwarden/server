using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.SqliteMigrations.Migrations
{
    /// <inheritdoc />
    public partial class RenamePamDaemonToAccessConnector : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "PamDaemon",
                newName: "PamAccessConnector");

            migrationBuilder.RenameTable(
                name: "PamDaemonTargetAssignment",
                newName: "PamAccessConnectorTargetAssignment");

            migrationBuilder.RenameColumn(
                name: "DaemonId",
                table: "PamAccessConnectorTargetAssignment",
                newName: "AccessConnectorId");

            migrationBuilder.RenameColumn(
                name: "ClaimedByDaemonId",
                table: "PamRotationJob",
                newName: "ClaimedByAccessConnectorId");

            migrationBuilder.RenameColumn(
                name: "ClaimedByDaemonId",
                table: "PamRotationAttempt",
                newName: "ClaimedByAccessConnectorId");

            migrationBuilder.RenameIndex(
                name: "IX_PamDaemon_ApiKeyId",
                table: "PamAccessConnector",
                newName: "IX_PamAccessConnector_ApiKeyId");

            migrationBuilder.RenameIndex(
                name: "IX_PamDaemon_OrganizationId",
                table: "PamAccessConnector",
                newName: "IX_PamAccessConnector_OrganizationId");

            migrationBuilder.RenameIndex(
                name: "IX_PamDaemonTargetAssignment_DaemonId_TargetSystemId",
                table: "PamAccessConnectorTargetAssignment",
                newName: "IX_PamAccessConnectorTargetAssignment_AccessConnectorId_TargetSystemId");

            migrationBuilder.RenameIndex(
                name: "IX_PamDaemonTargetAssignment_OrganizationId",
                table: "PamAccessConnectorTargetAssignment",
                newName: "IX_PamAccessConnectorTargetAssignment_OrganizationId");

            migrationBuilder.RenameIndex(
                name: "IX_PamDaemonTargetAssignment_TargetSystemId",
                table: "PamAccessConnectorTargetAssignment",
                newName: "IX_PamAccessConnectorTargetAssignment_TargetSystemId");

            migrationBuilder.RenameIndex(
                name: "IX_PamRotationJob_ClaimedByDaemonId_Status",
                table: "PamRotationJob",
                newName: "IX_PamRotationJob_ClaimedByAccessConnectorId_Status");

            migrationBuilder.RenameIndex(
                name: "IX_PamRotationAttempt_ClaimedByDaemonId_JobId",
                table: "PamRotationAttempt",
                newName: "IX_PamRotationAttempt_ClaimedByAccessConnectorId_JobId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "PamAccessConnector",
                newName: "PamDaemon");

            migrationBuilder.RenameTable(
                name: "PamAccessConnectorTargetAssignment",
                newName: "PamDaemonTargetAssignment");

            migrationBuilder.RenameColumn(
                name: "AccessConnectorId",
                table: "PamDaemonTargetAssignment",
                newName: "DaemonId");

            migrationBuilder.RenameColumn(
                name: "ClaimedByAccessConnectorId",
                table: "PamRotationJob",
                newName: "ClaimedByDaemonId");

            migrationBuilder.RenameColumn(
                name: "ClaimedByAccessConnectorId",
                table: "PamRotationAttempt",
                newName: "ClaimedByDaemonId");

            migrationBuilder.RenameIndex(
                name: "IX_PamAccessConnector_ApiKeyId",
                table: "PamDaemon",
                newName: "IX_PamDaemon_ApiKeyId");

            migrationBuilder.RenameIndex(
                name: "IX_PamAccessConnector_OrganizationId",
                table: "PamDaemon",
                newName: "IX_PamDaemon_OrganizationId");

            migrationBuilder.RenameIndex(
                name: "IX_PamAccessConnectorTargetAssignment_AccessConnectorId_TargetSystemId",
                table: "PamDaemonTargetAssignment",
                newName: "IX_PamDaemonTargetAssignment_DaemonId_TargetSystemId");

            migrationBuilder.RenameIndex(
                name: "IX_PamAccessConnectorTargetAssignment_OrganizationId",
                table: "PamDaemonTargetAssignment",
                newName: "IX_PamDaemonTargetAssignment_OrganizationId");

            migrationBuilder.RenameIndex(
                name: "IX_PamAccessConnectorTargetAssignment_TargetSystemId",
                table: "PamDaemonTargetAssignment",
                newName: "IX_PamDaemonTargetAssignment_TargetSystemId");

            migrationBuilder.RenameIndex(
                name: "IX_PamRotationJob_ClaimedByAccessConnectorId_Status",
                table: "PamRotationJob",
                newName: "IX_PamRotationJob_ClaimedByDaemonId_Status");

            migrationBuilder.RenameIndex(
                name: "IX_PamRotationAttempt_ClaimedByAccessConnectorId_JobId",
                table: "PamRotationAttempt",
                newName: "IX_PamRotationAttempt_ClaimedByDaemonId_JobId");
        }
    }
}
