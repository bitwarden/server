using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.PostgresMigrations.Migrations;

public partial class KDFOptions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "KdfMemory",
            table: "User",
            type: "integer",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "KdfParallelism",
            table: "User",
            type: "integer",
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
