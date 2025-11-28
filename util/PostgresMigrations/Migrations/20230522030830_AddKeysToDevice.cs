using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.PostgresMigrations.Migrations;

public partial class AddKeysToDevice : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "EncryptedPrivateKey",
            table: "Device",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "EncryptedPublicKey",
            table: "Device",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "EncryptedUserKey",
            table: "Device",
            type: "text",
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
