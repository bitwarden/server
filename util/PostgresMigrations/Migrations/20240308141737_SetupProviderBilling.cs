using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.PostgresMigrations.Migrations;

/// <inheritdoc />
public partial class SetupProviderBilling : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "ProviderId",
            table: "Transaction",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "GatewayCustomerId",
            table: "Provider",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "GatewaySubscriptionId",
            table: "Provider",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<byte>(
            name: "GatewayType",
            table: "Provider",
            type: "smallint",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "ProviderPlan",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ProviderId = table.Column<Guid>(type: "uuid", nullable: false),
                PlanType = table.Column<byte>(type: "smallint", nullable: false),
                SeatMinimum = table.Column<int>(type: "integer", nullable: true),
                PurchasedSeats = table.Column<int>(type: "integer", nullable: true),
                AllocatedSeats = table.Column<int>(type: "integer", nullable: true)
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
            });

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
