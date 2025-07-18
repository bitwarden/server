using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.MySqlMigrations.Migrations;

/// <inheritdoc />
public partial class Add_Organization_Subscription_Update : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "OrganizationSubscriptionUpdates",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                OrganizationId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                SeatsLastUpdated = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                SyncAttempts = table.Column<int>(type: "int", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OrganizationSubscriptionUpdates", x => x.Id);
                table.ForeignKey(
                    name: "FK_OrganizationSubscriptionUpdates_Organization_OrganizationId",
                    column: x => x.OrganizationId,
                    principalTable: "Organization",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            })
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateIndex(
            name: "IX_OrganizationSubscriptionUpdates_OrganizationId",
            table: "OrganizationSubscriptionUpdates",
            column: "OrganizationId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "OrganizationSubscriptionUpdates");
    }
}
