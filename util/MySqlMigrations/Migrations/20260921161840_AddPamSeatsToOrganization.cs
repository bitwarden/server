using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.MySqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddPamSeatsToOrganization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MaxAutoscalePamSeats",
                table: "Organization",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PamSeats",
                table: "Organization",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MaxAutoscalePamSeats",
                table: "Organization");

            migrationBuilder.DropColumn(
                name: "PamSeats",
                table: "Organization");
        }
    }
}
