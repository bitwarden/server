using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.MySqlMigrations.Migrations;

/// <inheritdoc />
public partial class AddOrganizationIdToApiKey : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
            name: "Key",
            table: "ApiKey",
            type: "longtext",
            nullable: true,
            oldClrType: typeof(string),
            oldType: "longtext")
            .Annotation("MySql:CharSet", "utf8mb4")
            .OldAnnotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.AlterColumn<string>(
            name: "EncryptedPayload",
            table: "ApiKey",
            type: "varchar(4000)",
            maxLength: 4000,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "varchar(4000)",
            oldMaxLength: 4000)
            .Annotation("MySql:CharSet", "utf8mb4")
            .OldAnnotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.AddColumn<Guid>(
            name: "OrganizationId",
            table: "ApiKey",
            type: "char(36)",
            nullable: true,
            collation: "ascii_general_ci");

        migrationBuilder.CreateIndex(
            name: "IX_ApiKey_OrganizationId",
            table: "ApiKey",
            column: "OrganizationId");

        migrationBuilder.AddCheckConstraint(
            name: "CK_ApiKey_SingleOwner",
            table: "ApiKey",
            sql: "`ServiceAccountId` IS NULL OR `OrganizationId` IS NULL");

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

        migrationBuilder.UpdateData(
            table: "ApiKey",
            keyColumn: "Key",
            keyValue: null,
            column: "Key",
            value: "");

        migrationBuilder.AlterColumn<string>(
            name: "Key",
            table: "ApiKey",
            type: "longtext",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "longtext",
            oldNullable: true)
            .Annotation("MySql:CharSet", "utf8mb4")
            .OldAnnotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.UpdateData(
            table: "ApiKey",
            keyColumn: "EncryptedPayload",
            keyValue: null,
            column: "EncryptedPayload",
            value: "");

        migrationBuilder.AlterColumn<string>(
            name: "EncryptedPayload",
            table: "ApiKey",
            type: "varchar(4000)",
            maxLength: 4000,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "varchar(4000)",
            oldMaxLength: 4000,
            oldNullable: true)
            .Annotation("MySql:CharSet", "utf8mb4")
            .OldAnnotation("MySql:CharSet", "utf8mb4");
    }
}
