using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.PostgresMigrations.Migrations;

/// <inheritdoc />
public partial class OrganizationUsersGetPremiumIndex : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Organization_Id_Enabled",
            table: "Organization");

        migrationBuilder.CreateIndex(
            name: "IX_Organization_Id_Enabled",
            table: "Organization",
            columns: new[] { "Id", "Enabled" })
            .Annotation("Npgsql:IndexInclude", new[] { "UseTotp", "UsersGetPremium" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Organization_Id_Enabled",
            table: "Organization");

        migrationBuilder.CreateIndex(
            name: "IX_Organization_Id_Enabled",
            table: "Organization",
            columns: new[] { "Id", "Enabled" })
            .Annotation("Npgsql:IndexInclude", new[] { "UseTotp" });
    }
}
