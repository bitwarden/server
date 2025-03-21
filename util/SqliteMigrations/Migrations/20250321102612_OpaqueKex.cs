using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.SqliteMigrations.Migrations;

/// <inheritdoc />
public partial class OpaqueKex : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "OpaqueKeyExchangeCredential",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                CipherConfiguration = table.Column<string>(type: "TEXT", nullable: true),
                CredentialBlob = table.Column<string>(type: "TEXT", nullable: true),
                EncryptedPublicKey = table.Column<string>(type: "TEXT", nullable: true),
                EncryptedPrivateKey = table.Column<string>(type: "TEXT", nullable: true),
                EncryptedUserKey = table.Column<string>(type: "TEXT", nullable: true),
                CreationDate = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OpaqueKeyExchangeCredential", x => x.Id);
            });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "OpaqueKeyExchangeCredential");
    }
}
