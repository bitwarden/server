using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.MySqlMigrations.Migrations;

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
                Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                OrganizationPartnershipId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                Status = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                RecordCount = table.Column<int>(type: "int", nullable: true),
                BlobName = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                CreationDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                CompletionDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                ExpirationDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                RevisionDate = table.Column<DateTime>(type: "datetime(6)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OrganizationPartnershipReconciliationExport", x => x.Id);
                table.ForeignKey(
                    name: "FK_OrganizationPartnershipReconciliationExport_OrganizationPart~",
                    column: x => x.OrganizationPartnershipId,
                    principalTable: "OrganizationPartnership",
                    principalColumn: "Id");
            })
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateIndex(
            name: "IX_OrganizationPartnershipEntitlement_OrganizationPartnershipId",
            table: "OrganizationPartnershipEntitlement",
            column: "OrganizationPartnershipId");

        migrationBuilder.CreateIndex(
            name: "IX_OrganizationPartnershipReconciliationExport_ExpirationDate",
            table: "OrganizationPartnershipReconciliationExport",
            column: "ExpirationDate");

        migrationBuilder.CreateIndex(
            name: "IX_OrganizationPartnershipReconciliationExport_OrganizationPart~",
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
