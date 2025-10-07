using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.MySqlMigrations.Migrations;

/// <inheritdoc />
public partial class addingSecretVerTable : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_SecretVersions_OrganizationUser_EditorOrganizationUserId",
            table: "SecretVersions");

        migrationBuilder.DropForeignKey(
            name: "FK_SecretVersions_Secret_SecretId",
            table: "SecretVersions");

        migrationBuilder.DropForeignKey(
            name: "FK_SecretVersions_ServiceAccount_EditorServiceAccountId",
            table: "SecretVersions");

        migrationBuilder.DropPrimaryKey(
            name: "PK_SecretVersions",
            table: "SecretVersions");

        migrationBuilder.RenameTable(
            name: "SecretVersions",
            newName: "SecretVersion");

        migrationBuilder.RenameIndex(
            name: "IX_SecretVersions_EditorServiceAccountId",
            table: "SecretVersion",
            newName: "IX_SecretVersion_EditorServiceAccountId");

        migrationBuilder.RenameIndex(
            name: "IX_SecretVersions_EditorOrganizationUserId",
            table: "SecretVersion",
            newName: "IX_SecretVersion_EditorOrganizationUserId");

        migrationBuilder.AddPrimaryKey(
            name: "PK_SecretVersion",
            table: "SecretVersion",
            column: "Id");

        migrationBuilder.AddForeignKey(
            name: "FK_SecretVersion_OrganizationUser_EditorOrganizationUserId",
            table: "SecretVersion",
            column: "EditorOrganizationUserId",
            principalTable: "OrganizationUser",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);

        migrationBuilder.AddForeignKey(
            name: "FK_SecretVersion_Secret_SecretId",
            table: "SecretVersion",
            column: "SecretId",
            principalTable: "Secret",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "FK_SecretVersion_ServiceAccount_EditorServiceAccountId",
            table: "SecretVersion",
            column: "EditorServiceAccountId",
            principalTable: "ServiceAccount",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_SecretVersion_OrganizationUser_EditorOrganizationUserId",
            table: "SecretVersion");

        migrationBuilder.DropForeignKey(
            name: "FK_SecretVersion_Secret_SecretId",
            table: "SecretVersion");

        migrationBuilder.DropForeignKey(
            name: "FK_SecretVersion_ServiceAccount_EditorServiceAccountId",
            table: "SecretVersion");

        migrationBuilder.DropPrimaryKey(
            name: "PK_SecretVersion",
            table: "SecretVersion");

        migrationBuilder.RenameTable(
            name: "SecretVersion",
            newName: "SecretVersions");

        migrationBuilder.RenameIndex(
            name: "IX_SecretVersion_EditorServiceAccountId",
            table: "SecretVersions",
            newName: "IX_SecretVersions_EditorServiceAccountId");

        migrationBuilder.RenameIndex(
            name: "IX_SecretVersion_EditorOrganizationUserId",
            table: "SecretVersions",
            newName: "IX_SecretVersions_EditorOrganizationUserId");

        migrationBuilder.AddPrimaryKey(
            name: "PK_SecretVersions",
            table: "SecretVersions",
            column: "Id");

        migrationBuilder.AddForeignKey(
            name: "FK_SecretVersions_OrganizationUser_EditorOrganizationUserId",
            table: "SecretVersions",
            column: "EditorOrganizationUserId",
            principalTable: "OrganizationUser",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);

        migrationBuilder.AddForeignKey(
            name: "FK_SecretVersions_Secret_SecretId",
            table: "SecretVersions",
            column: "SecretId",
            principalTable: "Secret",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "FK_SecretVersions_ServiceAccount_EditorServiceAccountId",
            table: "SecretVersions",
            column: "EditorServiceAccountId",
            principalTable: "ServiceAccount",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);
    }
}
