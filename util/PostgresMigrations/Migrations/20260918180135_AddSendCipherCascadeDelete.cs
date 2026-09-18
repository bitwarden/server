using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.PostgresMigrations.Migrations;

/// <inheritdoc />
public partial class AddSendCipherCascadeDelete : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "IX_Send_CipherId",
            table: "Send",
            column: "CipherId");

        migrationBuilder.AddForeignKey(
            name: "FK_Send_Cipher_CipherId",
            table: "Send",
            column: "CipherId",
            principalTable: "Cipher",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_Send_Cipher_CipherId",
            table: "Send");

        migrationBuilder.DropIndex(
            name: "IX_Send_CipherId",
            table: "Send");
    }
}
