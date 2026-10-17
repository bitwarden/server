using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.SqliteMigrations.Migrations;

/// <inheritdoc />
public partial class AddOrganizationPartnershipReconciliationExport : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "OrganizationPartnershipReconciliationExport",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                OrganizationPartnershipId = table.Column<Guid>(type: "TEXT", nullable: false),
                Status = table.Column<byte>(type: "INTEGER", nullable: false),
                RecordCount = table.Column<int>(type: "INTEGER", nullable: true),
                BlobName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                CreationDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                CompletionDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                ExpirationDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                RevisionDate = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OrganizationPartnershipReconciliationExport", x => x.Id);
                table.ForeignKey(
                    name: "FK_OrganizationPartnershipReconciliationExport_OrganizationPartnership_OrganizationPartnershipId",
                    column: x => x.OrganizationPartnershipId,
                    principalTable: "OrganizationPartnership",
                    principalColumn: "Id");
            });

        migrationBuilder.CreateIndex(
            name: "IX_OrganizationPartnershipEntitlement_OrganizationPartnershipId",
            table: "OrganizationPartnershipEntitlement",
            column: "OrganizationPartnershipId");

        migrationBuilder.CreateIndex(
            name: "IX_OrganizationPartnershipReconciliationExport_ExpirationDate",
            table: "OrganizationPartnershipReconciliationExport",
            column: "ExpirationDate");

        migrationBuilder.CreateIndex(
            name: "IX_OrganizationPartnershipReconciliationExport_OrganizationPartnershipId",
            table: "OrganizationPartnershipReconciliationExport",
            column: "OrganizationPartnershipId");

        migrationBuilder.CreateIndex(
            name: "IX_OrganizationPartnershipReconciliationExport_Status",
            table: "OrganizationPartnershipReconciliationExport",
            column: "Status");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "OrganizationPartnershipReconciliationExport");

        migrationBuilder.DropIndex(
            name: "IX_OrganizationPartnershipEntitlement_OrganizationPartnershipId",
            table: "OrganizationPartnershipEntitlement");
    }
}
