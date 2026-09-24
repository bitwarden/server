using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.SqliteMigrations.Migrations;

/// <inheritdoc />
public partial class ReshapeAccessLeaseCipherIndex : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_AccessLease_CipherId_Action",
            table: "AccessLease");

        migrationBuilder.CreateIndex(
            name: "IX_AccessLease_CipherId_Action_NotAfter",
            table: "AccessLease",
            columns: new[] { "CipherId", "Action", "NotAfter" },
            descending: new[] { false, false, true });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_AccessLease_CipherId_Action_NotAfter",
            table: "AccessLease");

        migrationBuilder.CreateIndex(
            name: "IX_AccessLease_CipherId_Action",
            table: "AccessLease",
            columns: new[] { "CipherId", "Action" });
    }
}
