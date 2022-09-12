using Microsoft.EntityFrameworkCore.Migrations;

namespace Bit.PostgresMigrations.Migrations;

public partial class KeyConnectorFlag : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "UseKeyConnector",
            table: "Organization",
            type: "boolean",
            nullable: false,
            defaultValue: false);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "UseKeyConnector",
            table: "Organization");
    }
}
