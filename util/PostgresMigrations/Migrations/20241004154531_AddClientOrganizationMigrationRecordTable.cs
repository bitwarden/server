using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.PostgresMigrations.Migrations;

/// <inheritdoc />
public partial class AddClientOrganizationMigrationRecordTable : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ClientOrganizationMigrationRecord",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                ProviderId = table.Column<Guid>(type: "uuid", nullable: false),
                PlanType = table.Column<byte>(type: "smallint", nullable: false),
                Seats = table.Column<int>(type: "integer", nullable: false),
                MaxStorageGb = table.Column<short>(type: "smallint", nullable: true),
                GatewayCustomerId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                GatewaySubscriptionId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                ExpirationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                MaxAutoscaleSeats = table.Column<int>(type: "integer", nullable: true),
                Status = table.Column<byte>(type: "smallint", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ClientOrganizationMigrationRecord", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ClientOrganizationMigrationRecord_ProviderId_OrganizationId",
            table: "ClientOrganizationMigrationRecord",
            columns: new[] { "ProviderId", "OrganizationId" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ClientOrganizationMigrationRecord");
    }
}
