using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.SqliteMigrations.Migrations;

public partial class KDFOptions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "KdfMemory",
            table: "User",
            type: "INTEGER",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "KdfParallelism",
            table: "User",
            type: "INTEGER",
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
