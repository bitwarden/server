using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.SqliteMigrations.Migrations;

/// <inheritdoc />
public partial class AccessLeaseCollectionIdActionNotAfterIndex : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_AccessLease_CollectionId_Action",
            table: "AccessLease");

        migrationBuilder.CreateIndex(
            name: "IX_AccessLease_CollectionId_Action_NotAfter",
            table: "AccessLease",
            columns: new[] { "CollectionId", "Action", "NotAfter" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_AccessLease_CollectionId_Action_NotAfter",
            table: "AccessLease");

        migrationBuilder.CreateIndex(
            name: "IX_AccessLease_CollectionId_Action",
            table: "AccessLease",
            columns: new[] { "CollectionId", "Action" });
    }
}
