using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.PostgresMigrations.Migrations;

/// <inheritdoc />
public partial class CreateSecretVersionsTable : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "SecretVersions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                SecretId = table.Column<Guid>(type: "uuid", nullable: false),
                Value = table.Column<string>(type: "text", nullable: false),
                VersionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                EditorServiceAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                EditorOrganizationUserId = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SecretVersions", x => x.Id);
                table.ForeignKey(
                    name: "FK_SecretVersions_OrganizationUser_EditorOrganizationUserId",
                    column: x => x.EditorOrganizationUserId,
                    principalTable: "OrganizationUser",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
                table.ForeignKey(
                    name: "FK_SecretVersions_Secret_SecretId",
                    column: x => x.SecretId,
                    principalTable: "Secret",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_SecretVersions_ServiceAccount_EditorServiceAccountId",
                    column: x => x.EditorServiceAccountId,
                    principalTable: "ServiceAccount",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateIndex(
            name: "IX_SecretVersion_SecretId",
            table: "SecretVersions",
            column: "SecretId");

        migrationBuilder.CreateIndex(
            name: "IX_SecretVersions_EditorOrganizationUserId",
            table: "SecretVersions",
            column: "EditorOrganizationUserId");

        migrationBuilder.CreateIndex(
            name: "IX_SecretVersions_EditorServiceAccountId",
            table: "SecretVersions",
            column: "EditorServiceAccountId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "SecretVersions");
    }
}
