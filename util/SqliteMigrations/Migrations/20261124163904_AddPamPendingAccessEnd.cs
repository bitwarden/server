using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.SqliteMigrations.Migrations;

/// <inheritdoc />
public partial class AddPamPendingAccessEnd : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "PamPendingAccessEnd",
            columns: table => new
            {
                AccessLeaseId = table.Column<Guid>(type: "TEXT", nullable: false),
                RotationConfigId = table.Column<Guid>(type: "TEXT", nullable: false),
                CreationDate = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PamPendingAccessEnd", x => x.AccessLeaseId);
                table.ForeignKey(
                    name: "FK_PamPendingAccessEnd_PamRotationConfig_RotationConfigId",
                    column: x => x.RotationConfigId,
                    principalTable: "PamRotationConfig",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_PamPendingAccessEnd_RotationConfigId",
            table: "PamPendingAccessEnd",
            column: "RotationConfigId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "PamPendingAccessEnd");
    }
}
