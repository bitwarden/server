using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.PostgresMigrations.Migrations;

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
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                PublicKey = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                CredentialId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                Counter = table.Column<int>(type: "integer", nullable: false),
                Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                AaGuid = table.Column<Guid>(type: "uuid", nullable: false),
                EncryptedUserKey = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                EncryptedPrivateKey = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                EncryptedPublicKey = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                SupportsPrf = table.Column<bool>(type: "boolean", nullable: false),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                RevisionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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
