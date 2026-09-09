using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.PostgresMigrations.Migrations;

/// <inheritdoc />
public partial class AddProviderIdToPlayItem : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "CK_PlayItem_UserOrOrganization",
            table: "PlayItem");

        migrationBuilder.AddColumn<Guid>(
            name: "ProviderId",
            table: "PlayItem",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_PlayItem_ProviderId",
            table: "PlayItem",
            column: "ProviderId");

        migrationBuilder.AddCheckConstraint(
            name: "CK_PlayItem_UserOrOrganizationOrProvider",
            table: "PlayItem",
            sql: "((CASE WHEN \"UserId\" IS NOT NULL THEN 1 ELSE 0 END) + (CASE WHEN \"OrganizationId\" IS NOT NULL THEN 1 ELSE 0 END) + (CASE WHEN \"ProviderId\" IS NOT NULL THEN 1 ELSE 0 END)) = 1");

        migrationBuilder.AddForeignKey(
            name: "FK_PlayItem_Provider_ProviderId",
            table: "PlayItem",
            column: "ProviderId",
            principalTable: "Provider",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_PlayItem_Provider_ProviderId",
            table: "PlayItem");

        migrationBuilder.DropIndex(
            name: "IX_PlayItem_ProviderId",
            table: "PlayItem");

        migrationBuilder.DropCheckConstraint(
            name: "CK_PlayItem_UserOrOrganizationOrProvider",
            table: "PlayItem");

        migrationBuilder.DropColumn(
            name: "ProviderId",
            table: "PlayItem");

        migrationBuilder.AddCheckConstraint(
            name: "CK_PlayItem_UserOrOrganization",
            table: "PlayItem",
            sql: "(\"UserId\" IS NOT NULL AND \"OrganizationId\" IS NULL) OR (\"UserId\" IS NULL AND \"OrganizationId\" IS NOT NULL)");
    }
}
