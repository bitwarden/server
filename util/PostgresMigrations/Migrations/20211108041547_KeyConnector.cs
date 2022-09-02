using Microsoft.EntityFrameworkCore.Migrations;

namespace Bit.PostgresMigrations.Migrations;

public partial class KeyConnector : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "UsesKeyConnector",
            table: "User",
            type: "boolean",
            nullable: false,
            defaultValue: false);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "UsesKeyConnector",
            table: "User");
    }
}
