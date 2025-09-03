using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.SqliteMigrations.Migrations;

/// <inheritdoc />
public partial class SecurityTaskCascadeDelete : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_SecurityTask_Cipher_CipherId",
            table: "SecurityTask");

        migrationBuilder.AddForeignKey(
            name: "FK_SecurityTask_Cipher_CipherId",
            table: "SecurityTask",
            column: "CipherId",
            principalTable: "Cipher",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_SecurityTask_Cipher_CipherId",
            table: "SecurityTask");

        migrationBuilder.AddForeignKey(
            name: "FK_SecurityTask_Cipher_CipherId",
            table: "SecurityTask",
            column: "CipherId",
            principalTable: "Cipher",
            principalColumn: "Id");
    }
}
