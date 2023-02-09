using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.SqliteMigrations.Migrations;

public partial class CipherKeyUpdate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "ForceKeyRotation",
            table: "Cipher",
            type: "INTEGER",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<string>(
            name: "Key",
            table: "Cipher",
            type: "TEXT",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "ForceKeyRotation",
            table: "Cipher");

        migrationBuilder.DropColumn(
            name: "Key",
            table: "Cipher");
    }
}
