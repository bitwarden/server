using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace Bit.MySqlMigrations.Migrations;

public partial class AvatarColor : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "AvatarColor",
            table: "User",
            type: "varchar(7)",
            maxLength: 7,
            nullable: true)
            .Annotation("MySql:CharSet", "utf8mb4");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "AvatarColor",
            table: "User");
    }
}
