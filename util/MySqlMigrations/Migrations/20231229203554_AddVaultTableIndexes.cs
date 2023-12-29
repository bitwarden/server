using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.MySqlMigrations.Migrations;

/// <inheritdoc />
public partial class AddVaultTableIndexes : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "IX_Cipher_DeletedDate",
            table: "Cipher",
            column: "DeletedDate");

        migrationBuilder.CreateIndex(
            name: "IX_Cipher_UserId_OrganizationId",
            table: "Cipher",
            columns: new[] { "UserId", "OrganizationId" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Cipher_DeletedDate",
            table: "Cipher");

        migrationBuilder.DropIndex(
            name: "IX_Cipher_UserId_OrganizationId",
            table: "Cipher");
    }
}
