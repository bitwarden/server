using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.SqliteMigrations.Migrations;

/// <inheritdoc />
public partial class CreateOrganizationPartnership : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "OrganizationPartnership",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                OrganizationId = table.Column<Guid>(type: "TEXT", nullable: false),
                Name = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                Status = table.Column<byte>(type: "INTEGER", nullable: false),
                SponsoredPlanType = table.Column<byte>(type: "INTEGER", nullable: false),
                BindingMode = table.Column<byte>(type: "INTEGER", nullable: false),
                IdentityBindingConfiguration = table.Column<string>(type: "TEXT", nullable: true),
                RegisteredReturnOrigins = table.Column<string>(type: "TEXT", nullable: false),
                CreationDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                RevisionDate = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OrganizationPartnership", x => x.Id);
                table.ForeignKey(
                    name: "FK_OrganizationPartnership_Organization_OrganizationId",
                    column: x => x.OrganizationId,
                    principalTable: "Organization",
                    principalColumn: "Id");
            });

        migrationBuilder.CreateTable(
            name: "OrganizationPartnershipEntitlement",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                OrganizationPartnershipId = table.Column<Guid>(type: "TEXT", nullable: false),
                ExternalId = table.Column<string>(type: "TEXT", nullable: false),
                ExternalIdHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                State = table.Column<byte>(type: "INTEGER", nullable: false),
                UserId = table.Column<Guid>(type: "TEXT", nullable: true),
                AccountRef = table.Column<Guid>(type: "TEXT", nullable: true),
                Metadata = table.Column<string>(type: "TEXT", nullable: true),
                BoundDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                SuspendedDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                CanceledDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                ResumeWindowExpirationDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                LastAppliedEffectiveDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                CreationDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                RevisionDate = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OrganizationPartnershipEntitlement", x => x.Id);
                table.ForeignKey(
                    name: "FK_OrganizationPartnershipEntitlement_OrganizationPartnership_OrganizationPartnershipId",
                    column: x => x.OrganizationPartnershipId,
                    principalTable: "OrganizationPartnership",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_OrganizationPartnershipEntitlement_User_UserId",
                    column: x => x.UserId,
                    principalTable: "User",
                    principalColumn: "Id");
            });

        migrationBuilder.CreateIndex(
            name: "IX_OrganizationPartnership_OrganizationId",
            table: "OrganizationPartnership",
            column: "OrganizationId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_OrganizationPartnershipEntitlement_OrganizationPartnershipId_ExternalIdHash",
            table: "OrganizationPartnershipEntitlement",
            columns: new[] { "OrganizationPartnershipId", "ExternalIdHash" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_OrganizationPartnershipEntitlement_State_ResumeWindowExpirationDate",
            table: "OrganizationPartnershipEntitlement",
            columns: new[] { "State", "ResumeWindowExpirationDate" });

        migrationBuilder.CreateIndex(
            name: "IX_OrganizationPartnershipEntitlement_UserId",
            table: "OrganizationPartnershipEntitlement",
            column: "UserId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "OrganizationPartnershipEntitlement");

        migrationBuilder.DropTable(
            name: "OrganizationPartnership");
    }
}
