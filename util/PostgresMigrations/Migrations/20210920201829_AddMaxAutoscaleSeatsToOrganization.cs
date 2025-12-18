using Microsoft.EntityFrameworkCore.Migrations;

namespace Bit.PostgresMigrations.Migrations;

public partial class AddMaxAutoscaleSeatsToOrganization : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "MaxAutoscaleSeats",
            table: "Organization",
            type: "integer",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "OwnersNotifiedOfAutoscaling",
            table: "Organization",
            type: "timestamp without time zone",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "ProviderOrganizationId",
            table: "Event",
            type: "uuid",
            nullable: true);
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
