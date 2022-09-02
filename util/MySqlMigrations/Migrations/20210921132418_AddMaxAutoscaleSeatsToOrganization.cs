using Microsoft.EntityFrameworkCore.Migrations;

namespace Bit.MySqlMigrations.Migrations;

public partial class AddMaxAutoscaleSeatsToOrganization : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "MaxAutoscaleSeats",
            table: "Organization",
            type: "int",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "OwnersNotifiedOfAutoscaling",
            table: "Organization",
            type: "datetime(6)",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "ProviderOrganizationId",
            table: "Event",
            type: "char(36)",
            nullable: true,
            collation: "ascii_general_ci");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "MaxAutoscaleSeats",
            table: "Organization");

        migrationBuilder.DropColumn(
            name: "OwnersNotifiedOfAutoscaling",
            table: "Organization");

        migrationBuilder.DropColumn(
            name: "ProviderOrganizationId",
            table: "Event");
    }
}
