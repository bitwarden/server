using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.MySqlMigrations.Migrations;

public partial class ProjectSecret : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ProjectSecret",
            columns: table => new
            {
                ProjectsId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                SecretsId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ProjectSecret", x => new { x.ProjectsId, x.SecretsId });
                table.ForeignKey(
                    name: "FK_ProjectSecret_Project_ProjectsId",
                    column: x => x.ProjectsId,
                    principalTable: "Project",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_ProjectSecret_Secret_SecretsId",
                    column: x => x.SecretsId,
                    principalTable: "Secret",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            })
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateIndex(
            name: "IX_ProjectSecret_SecretsId",
            table: "ProjectSecret",
            column: "SecretsId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ProjectSecret");
    }
}
