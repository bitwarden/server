using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.PostgresMigrations.Migrations;

/// <inheritdoc />
public partial class UserSignatureKeyPair : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "SignedPublicKey",
            table: "User",
            type: "text",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "UserSignatureKeyPair",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                SignatureAlgorithm = table.Column<byte>(type: "smallint", nullable: false),
                VerifyingKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                SigningKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                RevisionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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
            column: "UserId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "UserSignatureKeyPair");

        migrationBuilder.DropColumn(
            name: "SignedPublicKey",
            table: "User");
    }
}
