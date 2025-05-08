using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.PostgresMigrations.Migrations;

/// <inheritdoc />
public partial class SigningKeys : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "SignedPublicKeyOwnershipClaim",
            table: "User",
            type: "text",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "UserSigningKeys",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                KeyType = table.Column<byte>(type: "smallint", nullable: false),
                VerifyingKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                SigningKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                RevisionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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
