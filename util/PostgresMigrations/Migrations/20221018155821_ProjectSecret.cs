using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.PostgresMigrations.Migrations;

public partial class ProjectSecret : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ProjectSecret",
            columns: table => new
            {
                ProjectsId = table.Column<Guid>(type: "uuid", nullable: false),
                SecretsId = table.Column<Guid>(type: "uuid", nullable: false)
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
            });

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
