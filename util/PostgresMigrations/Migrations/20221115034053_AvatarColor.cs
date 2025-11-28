using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.PostgresMigrations.Migrations;

public partial class AvatarColor : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {

        migrationBuilder.AddColumn<string>(
            name: "AvatarColor",
            table: "User",
            type: "character varying(7)",
            maxLength: 7,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "AvatarColor",
            table: "User");
    }
}
