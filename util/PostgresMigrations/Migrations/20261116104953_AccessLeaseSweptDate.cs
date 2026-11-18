using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.PostgresMigrations.Migrations;

/// <inheritdoc />
public partial class AccessLeaseSweptDate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(
            name: "SweptDate",
            table: "AccessLease",
            type: "timestamp with time zone",
            nullable: true);

        // Carries the journal over so already-swept leases are not swept again.
        migrationBuilder.Sql(
            @"UPDATE ""AccessLease"" AS al SET ""SweptDate"" = s.""SweptDate"" FROM ""PamLeaseExpirySweep"" AS s WHERE s.""AccessLeaseId"" = al.""Id"";");

        migrationBuilder.CreateIndex(
            name: "IX_AccessRequest_RequesterId_CipherId_Action_NotAfter",
            table: "AccessRequest",
            columns: new[] { "RequesterId", "CipherId", "Action", "NotAfter" });

        migrationBuilder.CreateIndex(
            name: "IX_AccessLease_RequesterId_Action_NotAfter",
            table: "AccessLease",
            columns: new[] { "RequesterId", "Action", "NotAfter" });

        migrationBuilder.CreateIndex(
            name: "IX_AccessLease_SweptDate_Action_NotAfter",
            table: "AccessLease",
            columns: new[] { "SweptDate", "Action", "NotAfter" });

        migrationBuilder.DropIndex(
            name: "IX_AccessRequest_RequesterId_CipherId_Action",
            table: "AccessRequest");

        migrationBuilder.DropIndex(
            name: "IX_AccessLease_NotAfter_Action",
            table: "AccessLease");

        migrationBuilder.DropIndex(
            name: "IX_AccessLease_RequesterId_CipherId_Action",
            table: "AccessLease");

        migrationBuilder.DropTable(
            name: "PamLeaseExpirySweep");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "PamLeaseExpirySweep",
            columns: table => new
            {
                AccessLeaseId = table.Column<Guid>(type: "uuid", nullable: false),
                SweptDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PamLeaseExpirySweep", x => x.AccessLeaseId);
                table.ForeignKey(
                    name: "FK_PamLeaseExpirySweep_AccessLease_AccessLeaseId",
                    column: x => x.AccessLeaseId,
                    principalTable: "AccessLease",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_AccessRequest_RequesterId_CipherId_Action",
            table: "AccessRequest",
            columns: new[] { "RequesterId", "CipherId", "Action" });

        migrationBuilder.CreateIndex(
            name: "IX_AccessLease_NotAfter_Action",
            table: "AccessLease",
            columns: new[] { "NotAfter", "Action" });

        migrationBuilder.CreateIndex(
            name: "IX_AccessLease_RequesterId_CipherId_Action",
            table: "AccessLease",
            columns: new[] { "RequesterId", "CipherId", "Action" });

        migrationBuilder.DropIndex(
            name: "IX_AccessRequest_RequesterId_CipherId_Action_NotAfter",
            table: "AccessRequest");

        migrationBuilder.DropIndex(
            name: "IX_AccessLease_RequesterId_Action_NotAfter",
            table: "AccessLease");

        migrationBuilder.DropIndex(
            name: "IX_AccessLease_SweptDate_Action_NotAfter",
            table: "AccessLease");

        migrationBuilder.DropColumn(
            name: "SweptDate",
            table: "AccessLease");
    }
}
