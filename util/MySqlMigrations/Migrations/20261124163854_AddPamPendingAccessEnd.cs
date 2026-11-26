using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.MySqlMigrations.Migrations;

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
                AccessLeaseId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                RotationConfigId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                CreationDate = table.Column<DateTime>(type: "datetime(6)", nullable: false)
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
            })
            .Annotation("MySql:CharSet", "utf8mb4");

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
