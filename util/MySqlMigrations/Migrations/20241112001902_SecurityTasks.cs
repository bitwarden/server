using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.MySqlMigrations.Migrations;

/// <inheritdoc />
public partial class SecurityTasks : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "SecurityTask",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                OrganizationId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                CipherId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                Type = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                Status = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                CreationDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                RevisionDate = table.Column<DateTime>(type: "datetime(6)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SecurityTask", x => x.Id);
                table.ForeignKey(
                    name: "FK_SecurityTask_Cipher_CipherId",
                    column: x => x.CipherId,
                    principalTable: "Cipher",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_SecurityTask_Organization_OrganizationId",
                    column: x => x.OrganizationId,
                    principalTable: "Organization",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            })
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateIndex(
            name: "IX_SecurityTask_CipherId",
            table: "SecurityTask",
            column: "CipherId");

        migrationBuilder.CreateIndex(
            name: "IX_SecurityTask_OrganizationId",
            table: "SecurityTask",
            column: "OrganizationId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "SecurityTask");
    }
}
