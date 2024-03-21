using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.MySqlMigrations.Migrations;

/// <inheritdoc />
public partial class SetupProviderBilling : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "ProviderId",
            table: "Transaction",
            type: "char(36)",
            nullable: true,
            collation: "ascii_general_ci");

        migrationBuilder.AddColumn<string>(
            name: "GatewayCustomerId",
            table: "Provider",
            type: "longtext",
            nullable: true)
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.AddColumn<string>(
            name: "GatewaySubscriptionId",
            table: "Provider",
            type: "longtext",
            nullable: true)
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.AddColumn<byte>(
            name: "GatewayType",
            table: "Provider",
            type: "tinyint unsigned",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "ProviderPlan",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                ProviderId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                PlanType = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                SeatMinimum = table.Column<int>(type: "int", nullable: true),
                PurchasedSeats = table.Column<int>(type: "int", nullable: true),
                AllocatedSeats = table.Column<int>(type: "int", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ProviderPlan", x => x.Id);
                table.ForeignKey(
                    name: "FK_ProviderPlan_Provider_ProviderId",
                    column: x => x.ProviderId,
                    principalTable: "Provider",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            })
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateIndex(
            name: "IX_Transaction_ProviderId",
            table: "Transaction",
            column: "ProviderId");

        migrationBuilder.CreateIndex(
            name: "IX_ProviderPlan_Id_PlanType",
            table: "ProviderPlan",
            columns: new[] { "Id", "PlanType" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_ProviderPlan_ProviderId",
            table: "ProviderPlan",
            column: "ProviderId");

        migrationBuilder.AddForeignKey(
            name: "FK_Transaction_Provider_ProviderId",
            table: "Transaction",
            column: "ProviderId",
            principalTable: "Provider",
            principalColumn: "Id");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_Transaction_Provider_ProviderId",
            table: "Transaction");

        migrationBuilder.DropTable(
            name: "ProviderPlan");

        migrationBuilder.DropIndex(
            name: "IX_Transaction_ProviderId",
            table: "Transaction");

        migrationBuilder.DropColumn(
            name: "ProviderId",
            table: "Transaction");

        migrationBuilder.DropColumn(
            name: "GatewayCustomerId",
            table: "Provider");

        migrationBuilder.DropColumn(
            name: "GatewaySubscriptionId",
            table: "Provider");

        migrationBuilder.DropColumn(
            name: "GatewayType",
            table: "Provider");
    }
}
