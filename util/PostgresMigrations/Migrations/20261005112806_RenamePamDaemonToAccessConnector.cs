using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.PostgresMigrations.Migrations
{
    /// <inheritdoc />
    public partial class RenamePamDaemonToAccessConnector : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PamDaemon_ApiKey_ApiKeyId",
                table: "PamDaemon");

            migrationBuilder.DropForeignKey(
                name: "FK_PamDaemon_Organization_OrganizationId",
                table: "PamDaemon");

            migrationBuilder.DropForeignKey(
                name: "FK_PamDaemonTargetAssignment_Organization_OrganizationId",
                table: "PamDaemonTargetAssignment");

            migrationBuilder.DropForeignKey(
                name: "FK_PamDaemonTargetAssignment_PamDaemon_DaemonId",
                table: "PamDaemonTargetAssignment");

            migrationBuilder.DropForeignKey(
                name: "FK_PamDaemonTargetAssignment_PamTargetSystem_TargetSystemId",
                table: "PamDaemonTargetAssignment");

            migrationBuilder.DropPrimaryKey(
                name: "PK_PamDaemon",
                table: "PamDaemon");

            migrationBuilder.DropPrimaryKey(
                name: "PK_PamDaemonTargetAssignment",
                table: "PamDaemonTargetAssignment");

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
                newName: "IX_PamAccessConnectorTargetAssignment_AccessConnectorId_Target~");

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

            migrationBuilder.AddPrimaryKey(
                name: "PK_PamAccessConnector",
                table: "PamAccessConnector",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_PamAccessConnectorTargetAssignment",
                table: "PamAccessConnectorTargetAssignment",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PamAccessConnector_ApiKey_ApiKeyId",
                table: "PamAccessConnector",
                column: "ApiKeyId",
                principalTable: "ApiKey",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PamAccessConnector_Organization_OrganizationId",
                table: "PamAccessConnector",
                column: "OrganizationId",
                principalTable: "Organization",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PamAccessConnectorTargetAssignment_Organization_Organizatio~",
                table: "PamAccessConnectorTargetAssignment",
                column: "OrganizationId",
                principalTable: "Organization",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PamAccessConnectorTargetAssignment_PamAccessConnector_Acces~",
                table: "PamAccessConnectorTargetAssignment",
                column: "AccessConnectorId",
                principalTable: "PamAccessConnector",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PamAccessConnectorTargetAssignment_PamTargetSystem_TargetSy~",
                table: "PamAccessConnectorTargetAssignment",
                column: "TargetSystemId",
                principalTable: "PamTargetSystem",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PamAccessConnector_ApiKey_ApiKeyId",
                table: "PamAccessConnector");

            migrationBuilder.DropForeignKey(
                name: "FK_PamAccessConnector_Organization_OrganizationId",
                table: "PamAccessConnector");

            migrationBuilder.DropForeignKey(
                name: "FK_PamAccessConnectorTargetAssignment_Organization_Organizatio~",
                table: "PamAccessConnectorTargetAssignment");

            migrationBuilder.DropForeignKey(
                name: "FK_PamAccessConnectorTargetAssignment_PamAccessConnector_Acces~",
                table: "PamAccessConnectorTargetAssignment");

            migrationBuilder.DropForeignKey(
                name: "FK_PamAccessConnectorTargetAssignment_PamTargetSystem_TargetSy~",
                table: "PamAccessConnectorTargetAssignment");

            migrationBuilder.DropPrimaryKey(
                name: "PK_PamAccessConnector",
                table: "PamAccessConnector");

            migrationBuilder.DropPrimaryKey(
                name: "PK_PamAccessConnectorTargetAssignment",
                table: "PamAccessConnectorTargetAssignment");

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
                name: "IX_PamAccessConnectorTargetAssignment_AccessConnectorId_Target~",
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

            migrationBuilder.AddPrimaryKey(
                name: "PK_PamDaemon",
                table: "PamDaemon",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_PamDaemonTargetAssignment",
                table: "PamDaemonTargetAssignment",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PamDaemon_ApiKey_ApiKeyId",
                table: "PamDaemon",
                column: "ApiKeyId",
                principalTable: "ApiKey",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PamDaemon_Organization_OrganizationId",
                table: "PamDaemon",
                column: "OrganizationId",
                principalTable: "Organization",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PamDaemonTargetAssignment_Organization_OrganizationId",
                table: "PamDaemonTargetAssignment",
                column: "OrganizationId",
                principalTable: "Organization",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PamDaemonTargetAssignment_PamDaemon_DaemonId",
                table: "PamDaemonTargetAssignment",
                column: "DaemonId",
                principalTable: "PamDaemon",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PamDaemonTargetAssignment_PamTargetSystem_TargetSystemId",
                table: "PamDaemonTargetAssignment",
                column: "TargetSystemId",
                principalTable: "PamTargetSystem",
                principalColumn: "Id");
        }
    }
}
