using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.SqliteMigrations.Migrations;

/// <inheritdoc />
public partial class UserCryptoV2 : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "SecurityState",
            table: "User",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "SecurityVersion",
            table: "User",
            type: "INTEGER",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "SignedPublicKey",
            table: "User",
            type: "TEXT",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "UserSignatureKeyPair",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                SignatureAlgorithm = table.Column<byte>(type: "INTEGER", nullable: false),
                VerifyingKey = table.Column<string>(type: "TEXT", nullable: false),
                SigningKey = table.Column<string>(type: "TEXT", nullable: false),
                CreationDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                RevisionDate = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_UserSignatureKeyPair", x => x.Id);
                table.ForeignKey(
                    name: "FK_UserSignatureKeyPair_User_UserId",
                    column: x => x.UserId,
                    principalTable: "User",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_UserSignatureKeyPair_UserId",
            table: "UserSignatureKeyPair",
            column: "UserId",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "UserSignatureKeyPair");

        migrationBuilder.DropColumn(
            name: "SecurityState",
            table: "User");

        migrationBuilder.DropColumn(
            name: "SecurityVersion",
            table: "User");

        migrationBuilder.DropColumn(
            name: "SignedPublicKey",
            table: "User");
    }
}
