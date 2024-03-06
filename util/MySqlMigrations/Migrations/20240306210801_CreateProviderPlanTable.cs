using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.MySqlMigrations.Migrations;

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
