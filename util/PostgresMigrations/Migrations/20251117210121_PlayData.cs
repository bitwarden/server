using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.PostgresMigrations.Migrations;

/// <inheritdoc />
public partial class PlayData : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "Id",
            table: "PlayData",
            type: "uuid",
            nullable: false,
            defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

        migrationBuilder.AddColumn<DateTime>(
            name: "CreationDate",
            table: "PlayData",
            type: "timestamp with time zone",
            nullable: false,
            defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

        migrationBuilder.AddColumn<string>(
            name: "PlayId",
            table: "PlayData",
            type: "character varying(256)",
            maxLength: 256,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddPrimaryKey(
            name: "PK_PlayData",
            table: "PlayData",
            column: "Id");

        migrationBuilder.CreateIndex(
            name: "IX_PlayData_OrganizationId",
            table: "PlayData",
            column: "OrganizationId");

        migrationBuilder.CreateIndex(
            name: "IX_PlayData_PlayId",
            table: "PlayData",
            column: "PlayId");

        migrationBuilder.CreateIndex(
            name: "IX_PlayData_UserId",
            table: "PlayData",
            column: "UserId");

        migrationBuilder.AddCheckConstraint(
            name: "CK_PlayData_UserOrOrganization",
            table: "PlayData",
            sql: "([UserId] IS NOT NULL AND [OrganizationId] IS NULL) OR ([UserId] IS NULL AND [OrganizationId] IS NOT NULL)");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropPrimaryKey(
            name: "PK_PlayData",
            table: "PlayData");

        migrationBuilder.DropIndex(
            name: "IX_PlayData_OrganizationId",
            table: "PlayData");

        migrationBuilder.DropIndex(
            name: "IX_PlayData_PlayId",
            table: "PlayData");

        migrationBuilder.DropIndex(
            name: "IX_PlayData_UserId",
            table: "PlayData");

        migrationBuilder.DropCheckConstraint(
            name: "CK_PlayData_UserOrOrganization",
            table: "PlayData");

        migrationBuilder.DropColumn(
            name: "Id",
            table: "PlayData");

        migrationBuilder.DropColumn(
            name: "CreationDate",
            table: "PlayData");

        migrationBuilder.DropColumn(
            name: "PlayId",
            table: "PlayData");
    }
}
