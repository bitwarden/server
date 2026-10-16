using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.PostgresMigrations.Migrations;

/// <inheritdoc />
public partial class AddOrganizationIdToApiKey : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
            name: "Key",
            table: "ApiKey",
            type: "text",
            nullable: true,
            oldClrType: typeof(string),
            oldType: "text");

        migrationBuilder.AlterColumn<string>(
            name: "EncryptedPayload",
            table: "ApiKey",
            type: "character varying(4000)",
            maxLength: 4000,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "character varying(4000)",
            oldMaxLength: 4000);

        migrationBuilder.AddColumn<Guid>(
            name: "OrganizationId",
            table: "ApiKey",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_ApiKey_OrganizationId",
            table: "ApiKey",
            column: "OrganizationId");

        migrationBuilder.AddCheckConstraint(
            name: "CK_ApiKey_SingleOwner",
            table: "ApiKey",
            sql: "\"ServiceAccountId\" IS NULL OR \"OrganizationId\" IS NULL");

        migrationBuilder.AddForeignKey(
            name: "FK_ApiKey_Organization_OrganizationId",
            table: "ApiKey",
            column: "OrganizationId",
            principalTable: "Organization",
            principalColumn: "Id");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_ApiKey_Organization_OrganizationId",
            table: "ApiKey");

        migrationBuilder.DropIndex(
            name: "IX_ApiKey_OrganizationId",
            table: "ApiKey");

        migrationBuilder.DropCheckConstraint(
            name: "CK_ApiKey_SingleOwner",
            table: "ApiKey");

        migrationBuilder.DropColumn(
            name: "OrganizationId",
            table: "ApiKey");

        migrationBuilder.AlterColumn<string>(
            name: "Key",
            table: "ApiKey",
            type: "text",
            nullable: false,
            defaultValue: "",
            oldClrType: typeof(string),
            oldType: "text",
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "EncryptedPayload",
            table: "ApiKey",
            type: "character varying(4000)",
            maxLength: 4000,
            nullable: false,
            defaultValue: "",
            oldClrType: typeof(string),
            oldType: "character varying(4000)",
            oldMaxLength: 4000,
            oldNullable: true);
    }
}
