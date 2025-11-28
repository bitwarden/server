using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.MySqlMigrations.Migrations;

/// <inheritdoc />
public partial class AddToolsTableIndexes : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "IX_Send_DeletionDate",
            table: "Send",
            column: "DeletionDate");

        migrationBuilder.CreateIndex(
            name: "IX_Send_UserId_OrganizationId",
            table: "Send",
            columns: new[] { "UserId", "OrganizationId" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Send_DeletionDate",
            table: "Send");

        migrationBuilder.DropIndex(
            name: "IX_Send_UserId_OrganizationId",
            table: "Send");
    }
}
