using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.PostgresMigrations.Migrations;

/// <inheritdoc />
public partial class AddVaultTableIndexes : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Folder_UserId",
            table: "Folder");

        migrationBuilder.CreateIndex(
            name: "IX_Folder_UserId",
            table: "Folder",
            column: "UserId")
            .Annotation("Npgsql:IndexInclude", new[] { "Name", "CreationDate", "RevisionDate" });

        migrationBuilder.CreateIndex(
            name: "IX_Cipher_DeletedDate",
            table: "Cipher",
            column: "DeletedDate");

        migrationBuilder.CreateIndex(
            name: "IX_Cipher_UserId_OrganizationId",
            table: "Cipher",
            columns: new[] { "UserId", "OrganizationId" })
            .Annotation("Npgsql:IndexInclude", new[] { "Type", "Data", "Favorites", "Folders", "Attachments", "CreationDate", "RevisionDate", "DeletedDate" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Folder_UserId",
            table: "Folder");

        migrationBuilder.DropIndex(
            name: "IX_Cipher_DeletedDate",
            table: "Cipher");

        migrationBuilder.DropIndex(
            name: "IX_Cipher_UserId_OrganizationId",
            table: "Cipher");

        migrationBuilder.CreateIndex(
            name: "IX_Folder_UserId",
            table: "Folder",
            column: "UserId");
    }
}
