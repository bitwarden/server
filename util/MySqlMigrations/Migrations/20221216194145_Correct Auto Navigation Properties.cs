using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.MySqlMigrations.Migrations;

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
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "UserId",
            table: "GroupUser",
            type: "char(36)",
            nullable: true,
            collation: "ascii_general_ci");

        migrationBuilder.AddColumn<Guid>(
            name: "UserId",
            table: "CollectionUsers",
            type: "char(36)",
            nullable: true,
            collation: "ascii_general_ci");

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
