using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.PostgresMigrations.Migrations;

/// <inheritdoc />
public partial class PamRotationDaemonActivity : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "IX_PamRotationAttempt_ClaimedByDaemonId_JobId",
            table: "PamRotationAttempt",
            columns: new[] { "ClaimedByDaemonId", "JobId" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_PamRotationAttempt_ClaimedByDaemonId_JobId",
            table: "PamRotationAttempt");
    }
}
