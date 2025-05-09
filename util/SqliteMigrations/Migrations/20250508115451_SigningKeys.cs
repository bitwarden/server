using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.SqliteMigrations.Migrations;

/// <inheritdoc />
public partial class SigningKeys : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "SignedPublicKeyOwnershipClaim",
            table: "User",
            type: "TEXT",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "UserSigningKeys",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                KeyType = table.Column<byte>(type: "INTEGER", nullable: false),
                VerifyingKey = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                SigningKey = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                CreationDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                RevisionDate = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_UserSigningKeys", x => x.Id);
                table.ForeignKey(
                    name: "FK_UserSigningKeys_User_UserId",
                    column: x => x.UserId,
                    principalTable: "User",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_UserSigningKeys_UserId",
            table: "UserSigningKeys",
            column: "UserId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "UserSigningKeys");

        migrationBuilder.DropColumn(
            name: "SignedPublicKeyOwnershipClaim",
            table: "User");
    }
}
