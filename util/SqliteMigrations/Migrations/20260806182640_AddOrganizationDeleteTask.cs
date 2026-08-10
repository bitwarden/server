using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.SqliteMigrations.Migrations;

/// <inheritdoc />
public partial class AddOrganizationDeleteTask : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "OrganizationDeleteTask",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                OrganizationId = table.Column<Guid>(type: "TEXT", nullable: false),
                TaskType = table.Column<byte>(type: "INTEGER", nullable: false),
                CreationDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                RevisionDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                StartDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                CompletedDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                ItemsDeletedCount = table.Column<long>(type: "INTEGER", nullable: false),
                FailureCount = table.Column<int>(type: "INTEGER", nullable: false),
                LastError = table.Column<string>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OrganizationDeleteTask", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_OrganizationDeleteTask_CompletedDate_CreationDate",
            table: "OrganizationDeleteTask",
            columns: new[] { "CompletedDate", "CreationDate" });

        migrationBuilder.CreateIndex(
            name: "IX_OrganizationDeleteTask_Id",
            table: "OrganizationDeleteTask",
            column: "Id");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "OrganizationDeleteTask");
    }
}
