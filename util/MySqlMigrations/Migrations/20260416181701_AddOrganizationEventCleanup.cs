using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.MySqlMigrations.Migrations;

/// <inheritdoc />
public partial class AddOrganizationEventCleanup : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "OrganizationEventCleanup",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                OrganizationId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                QueuedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                StartedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                LastProgressAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                CompletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                EventsDeletedCount = table.Column<long>(type: "bigint", nullable: false),
                Attempts = table.Column<int>(type: "int", nullable: false),
                LastError = table.Column<string>(type: "longtext", nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OrganizationEventCleanup", x => x.Id);
            })
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateIndex(
            name: "IX_OrganizationEventCleanup_CompletedAt_QueuedAt",
            table: "OrganizationEventCleanup",
            columns: new[] { "CompletedAt", "QueuedAt" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "OrganizationEventCleanup");
    }
}
