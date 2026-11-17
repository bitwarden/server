using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.MySqlMigrations.Migrations;

/// <inheritdoc />
public partial class AccessLeaseSweptDate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(
            name: "SweptDate",
            table: "AccessLease",
            type: "datetime(6)",
            nullable: true);

        // Carries the journal over so already-swept leases are not swept again.
        migrationBuilder.Sql(
            @"UPDATE `AccessLease` AS AL INNER JOIN `PamLeaseExpirySweep` AS S ON S.`AccessLeaseId` = AL.`Id` SET AL.`SweptDate` = S.`SweptDate`;");

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
                AccessLeaseId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                SweptDate = table.Column<DateTime>(type: "datetime(6)", nullable: false)
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
            })
            .Annotation("MySql:CharSet", "utf8mb4");

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
