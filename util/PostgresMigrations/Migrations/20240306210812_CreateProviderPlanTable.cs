using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.PostgresMigrations.Migrations;

/// <inheritdoc />
public partial class CreateProviderPlanTable : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
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
            name: "IX_ProviderPlan_ProviderId",
            table: "ProviderPlan",
            column: "ProviderId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ProviderPlan");
    }
}
