using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.MySqlMigrations.Migrations;

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
                Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                OrganizationId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                Name = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                Status = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                SponsoredPlanType = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                BindingMode = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                IdentityBindingConfiguration = table.Column<string>(type: "longtext", nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                RegisteredReturnOrigins = table.Column<string>(type: "longtext", nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                CreationDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                RevisionDate = table.Column<DateTime>(type: "datetime(6)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OrganizationPartnership", x => x.Id);
                table.ForeignKey(
                    name: "FK_OrganizationPartnership_Organization_OrganizationId",
                    column: x => x.OrganizationId,
                    principalTable: "Organization",
                    principalColumn: "Id");
            })
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateTable(
            name: "OrganizationPartnershipEntitlement",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                OrganizationPartnershipId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                ExternalId = table.Column<string>(type: "longtext", nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                ExternalIdHash = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                State = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                UserId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                AccountRef = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                Metadata = table.Column<string>(type: "longtext", nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                BoundDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                SuspendedDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                CanceledDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                ResumeWindowExpirationDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                LastAppliedEffectiveDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                CreationDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                RevisionDate = table.Column<DateTime>(type: "datetime(6)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OrganizationPartnershipEntitlement", x => x.Id);
                table.ForeignKey(
                    name: "FK_OrganizationPartnershipEntitlement_OrganizationPartnership_O~",
                    column: x => x.OrganizationPartnershipId,
                    principalTable: "OrganizationPartnership",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_OrganizationPartnershipEntitlement_User_UserId",
                    column: x => x.UserId,
                    principalTable: "User",
                    principalColumn: "Id");
            })
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateIndex(
            name: "IX_OrganizationPartnership_OrganizationId",
            table: "OrganizationPartnership",
            column: "OrganizationId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_OrganizationPartnershipEntitlement_OrganizationPartnershipId~",
            table: "OrganizationPartnershipEntitlement",
            columns: new[] { "OrganizationPartnershipId", "ExternalIdHash" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_OrganizationPartnershipEntitlement_State_ResumeWindowExpirat~",
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
