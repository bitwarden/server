using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.PostgresMigrations.Migrations
{
    /// <inheritdoc />
    public partial class CreateOpaqueKeyExchangeCredential : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OpaqueKeyExchangeCredential",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CipherConfiguration = table.Column<string>(type: "text", nullable: true),
                    CredentialBlob = table.Column<string>(type: "text", nullable: true),
                    EncryptedPublicKey = table.Column<string>(type: "text", nullable: true),
                    EncryptedPrivateKey = table.Column<string>(type: "text", nullable: true),
                    EncryptedUserKey = table.Column<string>(type: "text", nullable: true),
                    CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpaqueKeyExchangeCredential", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OpaqueKeyExchangeCredential_User_UserId",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OpaqueKeyExchangeCredential_UserId",
                table: "OpaqueKeyExchangeCredential",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OpaqueKeyExchangeCredential");
        }
    }
}
