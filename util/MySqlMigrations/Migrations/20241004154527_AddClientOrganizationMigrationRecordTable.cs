using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.MySqlMigrations.Migrations;

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
                Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                OrganizationId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                ProviderId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                PlanType = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                Seats = table.Column<int>(type: "int", nullable: false),
                MaxStorageGb = table.Column<short>(type: "smallint", nullable: true),
                GatewayCustomerId = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                GatewaySubscriptionId = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                ExpirationDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                MaxAutoscaleSeats = table.Column<int>(type: "int", nullable: true),
                Status = table.Column<byte>(type: "tinyint unsigned", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ClientOrganizationMigrationRecord", x => x.Id);
            })
            .Annotation("MySql:CharSet", "utf8mb4");

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
