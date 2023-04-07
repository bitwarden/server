using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.MySqlMigrations.Migrations;

public partial class KDFOptions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "KdfMemory",
            table: "User",
            type: "int",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "KdfParallelism",
            table: "User",
            type: "int",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "KdfMemory",
            table: "User");

        migrationBuilder.DropColumn(
            name: "KdfParallelism",
            table: "User");
    }
}
