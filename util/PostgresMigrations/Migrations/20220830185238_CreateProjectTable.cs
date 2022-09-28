using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.PostgresMigrations.Migrations;

public partial class CreateProjectTable : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Project",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "text", nullable: true),
                CreationDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                RevisionDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                DeletedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Project", x => x.Id);
                table.ForeignKey(
                    name: "FK_Project_Organization_OrganizationId",
                    column: x => x.OrganizationId,
                    principalTable: "Organization",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Project_DeletedDate",
            table: "Project",
            column: "DeletedDate");

        migrationBuilder.CreateIndex(
            name: "IX_Project_OrganizationId",
            table: "Project",
            column: "OrganizationId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "Project");
    }
}
