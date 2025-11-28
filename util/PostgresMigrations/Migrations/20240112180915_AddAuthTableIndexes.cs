using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.PostgresMigrations.Migrations;

/// <inheritdoc />
public partial class AddAuthTableIndexes : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "IX_SsoUser_OrganizationId_ExternalId",
            table: "SsoUser",
            columns: new[] { "OrganizationId", "ExternalId" },
            unique: true)
            .Annotation("Npgsql:IndexInclude", new[] { "UserId" });

        migrationBuilder.CreateIndex(
            name: "IX_SsoUser_OrganizationId_UserId",
            table: "SsoUser",
            columns: new[] { "OrganizationId", "UserId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Grant_ExpirationDate",
            table: "Grant",
            column: "ExpirationDate");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_SsoUser_OrganizationId_ExternalId",
            table: "SsoUser");

        migrationBuilder.DropIndex(
            name: "IX_SsoUser_OrganizationId_UserId",
            table: "SsoUser");

        migrationBuilder.DropIndex(
            name: "IX_Grant_ExpirationDate",
            table: "Grant");
    }
}
