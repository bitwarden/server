using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.SqliteMigrations.Migrations
{
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
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "CollectionUsers",
                type: "TEXT",
                nullable: true);

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
}
