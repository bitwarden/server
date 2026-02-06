using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.SqliteMigrations.Migrations;

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
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                OrganizationId = table.Column<Guid>(type: "TEXT", nullable: false),
                ProviderId = table.Column<Guid>(type: "TEXT", nullable: false),
                PlanType = table.Column<byte>(type: "INTEGER", nullable: false),
                Seats = table.Column<int>(type: "INTEGER", nullable: false),
                MaxStorageGb = table.Column<short>(type: "INTEGER", nullable: true),
                GatewayCustomerId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                GatewaySubscriptionId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                ExpirationDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                MaxAutoscaleSeats = table.Column<int>(type: "INTEGER", nullable: true),
                Status = table.Column<byte>(type: "INTEGER", nullable: false)
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
