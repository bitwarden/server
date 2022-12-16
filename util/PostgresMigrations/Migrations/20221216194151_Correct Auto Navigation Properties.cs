using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.PostgresMigrations.Migrations;

public partial class CorrectAutoNavigationProperties : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_CollectionUsers_User_UserId",
            table: "CollectionUsers");

        migrationBuilder.DropForeignKey(
            name: "FK_GroupUser_User_UserId",
            table: "GroupUser");

        migrationBuilder.DropIndex(
            name: "IX_GroupUser_UserId",
            table: "GroupUser");

        migrationBuilder.DropIndex(
            name: "IX_CollectionUsers_UserId",
            table: "CollectionUsers");

        migrationBuilder.DropColumn(
            name: "UserId",
            table: "GroupUser");

        migrationBuilder.DropColumn(
            name: "UserId",
            table: "CollectionUsers");

        migrationBuilder.AlterColumn<string>(
            name: "RequestIpAddress",
            table: "AuthRequest",
            type: "character varying(50)",
            maxLength: 50,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "text",
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "RequestDeviceIdentifier",
            table: "AuthRequest",
            type: "character varying(50)",
            maxLength: 50,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "text",
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "AccessCode",
            table: "AuthRequest",
            type: "character varying(25)",
            maxLength: 25,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "text",
            oldNullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "UserId",
            table: "GroupUser",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "UserId",
            table: "CollectionUsers",
            type: "uuid",
            nullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "RequestIpAddress",
            table: "AuthRequest",
            type: "text",
            nullable: true,
            oldClrType: typeof(string),
            oldType: "character varying(50)",
            oldMaxLength: 50,
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "RequestDeviceIdentifier",
            table: "AuthRequest",
            type: "text",
            nullable: true,
            oldClrType: typeof(string),
            oldType: "character varying(50)",
            oldMaxLength: 50,
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "AccessCode",
            table: "AuthRequest",
            type: "text",
            nullable: true,
            oldClrType: typeof(string),
            oldType: "character varying(25)",
            oldMaxLength: 25,
            oldNullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_GroupUser_UserId",
            table: "GroupUser",
            column: "UserId");

        migrationBuilder.CreateIndex(
            name: "IX_CollectionUsers_UserId",
            table: "CollectionUsers",
            column: "UserId");

        migrationBuilder.AddForeignKey(
            name: "FK_CollectionUsers_User_UserId",
            table: "CollectionUsers",
            column: "UserId",
            principalTable: "User",
            principalColumn: "Id");

        migrationBuilder.AddForeignKey(
            name: "FK_GroupUser_User_UserId",
            table: "GroupUser",
            column: "UserId",
            principalTable: "User",
            principalColumn: "Id");
    }
}
