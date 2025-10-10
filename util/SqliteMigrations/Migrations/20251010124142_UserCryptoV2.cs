using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.SqliteMigrations.Migrations;

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

        migrationBuilder.AddColumn<string>(
            name: "ApplicationData",
            table: "OrganizationReport",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "SummaryData",
            table: "OrganizationReport",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "GrantedServiceAccountId",
            table: "Event",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "ProjectId",
            table: "Event",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "ArchivedDate",
            table: "Cipher",
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
