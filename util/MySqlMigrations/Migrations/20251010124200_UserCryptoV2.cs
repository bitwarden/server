using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.MySqlMigrations.Migrations;

/// <inheritdoc />
public partial class UserCryptoV2 : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.RenameColumn(
            name: "Date",
            table: "OrganizationReport",
            newName: "RevisionDate");

        migrationBuilder.RenameIndex(
            name: "IX_Event_Date_OrganizationId_ActingUserId_CipherId",
            table: "Event",
            newName: "IX_Event_DateOrganizationIdUserId");

        migrationBuilder.AddColumn<string>(
            name: "SecurityState",
            table: "User",
            type: "longtext",
            nullable: true)
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.AddColumn<int>(
            name: "SecurityVersion",
            table: "User",
            type: "int",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "SignedPublicKey",
            table: "User",
            type: "longtext",
            nullable: true)
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.AddColumn<string>(
            name: "ApplicationData",
            table: "OrganizationReport",
            type: "longtext",
            nullable: true)
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.AddColumn<string>(
            name: "SummaryData",
            table: "OrganizationReport",
            type: "longtext",
            nullable: true)
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.AddColumn<Guid>(
            name: "GrantedServiceAccountId",
            table: "Event",
            type: "char(36)",
            nullable: true,
            collation: "ascii_general_ci");

        migrationBuilder.AddColumn<Guid>(
            name: "ProjectId",
            table: "Event",
            type: "char(36)",
            nullable: true,
            collation: "ascii_general_ci");

        migrationBuilder.AddColumn<DateTime>(
            name: "ArchivedDate",
            table: "Cipher",
            type: "datetime(6)",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "UserSignatureKeyPair",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                UserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                SignatureAlgorithm = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                VerifyingKey = table.Column<string>(type: "longtext", nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                SigningKey = table.Column<string>(type: "longtext", nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                CreationDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                RevisionDate = table.Column<DateTime>(type: "datetime(6)", nullable: false)
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
            })
            .Annotation("MySql:CharSet", "utf8mb4");

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
            name: "SecurityState",
            table: "User");

        migrationBuilder.DropColumn(
            name: "SecurityVersion",
            table: "User");

        migrationBuilder.DropColumn(
            name: "SignedPublicKey",
            table: "User");

        migrationBuilder.DropColumn(
            name: "ApplicationData",
            table: "OrganizationReport");

        migrationBuilder.DropColumn(
            name: "SummaryData",
            table: "OrganizationReport");

        migrationBuilder.DropColumn(
            name: "GrantedServiceAccountId",
            table: "Event");

        migrationBuilder.DropColumn(
            name: "ProjectId",
            table: "Event");

        migrationBuilder.DropColumn(
            name: "ArchivedDate",
            table: "Cipher");

        migrationBuilder.RenameColumn(
            name: "RevisionDate",
            table: "OrganizationReport",
            newName: "Date");

        migrationBuilder.RenameIndex(
            name: "IX_Event_DateOrganizationIdUserId",
            table: "Event",
            newName: "IX_Event_Date_OrganizationId_ActingUserId_CipherId");
    }
}
