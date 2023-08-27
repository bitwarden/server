using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.SqliteMigrations.Migrations;

public partial class AddKeysToDevice : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "EncryptedPrivateKey",
            table: "Device",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "EncryptedPublicKey",
            table: "Device",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "EncryptedUserKey",
            table: "Device",
            type: "TEXT",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "EncryptedPrivateKey",
            table: "Device");

        migrationBuilder.DropColumn(
            name: "EncryptedPublicKey",
            table: "Device");

        migrationBuilder.DropColumn(
            name: "EncryptedUserKey",
            table: "Device");
    }
}
