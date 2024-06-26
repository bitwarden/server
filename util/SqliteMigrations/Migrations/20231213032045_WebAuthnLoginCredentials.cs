using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.SqliteMigrations.Migrations;

/// <inheritdoc />
public partial class WebAuthnLoginCredentials : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "WebAuthnCredential",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                Name = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                PublicKey = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                CredentialId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                Counter = table.Column<int>(type: "INTEGER", nullable: false),
                Type = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                AaGuid = table.Column<Guid>(type: "TEXT", nullable: false),
                EncryptedUserKey = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                EncryptedPrivateKey = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                EncryptedPublicKey = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                SupportsPrf = table.Column<bool>(type: "INTEGER", nullable: false),
                CreationDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                RevisionDate = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_WebAuthnCredential", x => x.Id);
                table.ForeignKey(
                    name: "FK_WebAuthnCredential_User_UserId",
                    column: x => x.UserId,
                    principalTable: "User",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_WebAuthnCredential_UserId",
            table: "WebAuthnCredential",
            column: "UserId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "WebAuthnCredential");
    }
}
