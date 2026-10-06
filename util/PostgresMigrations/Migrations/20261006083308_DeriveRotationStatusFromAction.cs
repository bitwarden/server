using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.PostgresMigrations.Migrations;

/// <inheritdoc />
public partial class DeriveRotationStatusFromAction : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.RenameColumn(
            name: "Status",
            table: "PamRotationJob",
            newName: "Action");

        migrationBuilder.RenameIndex(
            name: "IX_PamRotationJob_Status_ExpiresAt",
            table: "PamRotationJob",
            newName: "IX_PamRotationJob_Action_ExpiresAt");

        migrationBuilder.RenameIndex(
            name: "IX_PamRotationJob_RotationConfigId_Status",
            table: "PamRotationJob",
            newName: "IX_PamRotationJob_RotationConfigId_Action");

        migrationBuilder.RenameIndex(
            name: "IX_PamRotationJob_ClaimedByAccessConnectorId_Status",
            table: "PamRotationJob",
            newName: "IX_PamRotationJob_ClaimedByAccessConnectorId_Action");

        migrationBuilder.RenameColumn(
            name: "Status",
            table: "PamRotationAttempt",
            newName: "Action");

        migrationBuilder.RenameIndex(
            name: "IX_PamRotationAttempt_JobId_Status",
            table: "PamRotationAttempt",
            newName: "IX_PamRotationAttempt_JobId_Action");

        migrationBuilder.CreateTable(
            name: "PamRotationJobTimeoutSweep",
            columns: table => new
            {
                RotationJobId = table.Column<Guid>(type: "uuid", nullable: false),
                SweptDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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

        // The old sweep already reported these timeouts; journaled so the new one doesn't report them again.
        migrationBuilder.Sql(
            "INSERT INTO \"PamRotationJobTimeoutSweep\" (\"RotationJobId\", \"SweptDate\") SELECT \"Id\", \"ExpiresAt\" FROM \"PamRotationJob\" WHERE \"Action\" = 4");

        // TimedOut and Abandoned have no Action; None re-derives the same status.
        migrationBuilder.Sql("UPDATE \"PamRotationJob\" SET \"Action\" = 0 WHERE \"Action\" = 4");
        migrationBuilder.Sql("UPDATE \"PamRotationAttempt\" SET \"Action\" = 0 WHERE \"Action\" = 3");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "PamRotationJobTimeoutSweep");

        migrationBuilder.RenameColumn(
            name: "Action",
            table: "PamRotationJob",
            newName: "Status");

        migrationBuilder.RenameIndex(
            name: "IX_PamRotationJob_RotationConfigId_Action",
            table: "PamRotationJob",
            newName: "IX_PamRotationJob_RotationConfigId_Status");

        migrationBuilder.RenameIndex(
            name: "IX_PamRotationJob_ClaimedByAccessConnectorId_Action",
            table: "PamRotationJob",
            newName: "IX_PamRotationJob_ClaimedByAccessConnectorId_Status");

        migrationBuilder.RenameIndex(
            name: "IX_PamRotationJob_Action_ExpiresAt",
            table: "PamRotationJob",
            newName: "IX_PamRotationJob_Status_ExpiresAt");

        migrationBuilder.RenameColumn(
            name: "Action",
            table: "PamRotationAttempt",
            newName: "Status");

        migrationBuilder.RenameIndex(
            name: "IX_PamRotationAttempt_JobId_Action",
            table: "PamRotationAttempt",
            newName: "IX_PamRotationAttempt_JobId_Status");
    }
}
