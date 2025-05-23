using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.PostgresMigrations.Migrations;

/// <inheritdoc />
public partial class SsoExternalId : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
            name: "ExternalId",
            table: "SsoUser",
            type: "character varying(300)",
            maxLength: 300,
            nullable: true,
            collation: "postgresIndetermanisticCollation",
            oldClrType: typeof(string),
            oldType: "character varying(50)",
            oldMaxLength: 50,
            oldNullable: true,
            oldCollation: "postgresIndetermanisticCollation");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
            name: "ExternalId",
            table: "SsoUser",
            type: "character varying(50)",
            maxLength: 50,
            nullable: true,
            collation: "postgresIndetermanisticCollation",
            oldClrType: typeof(string),
            oldType: "character varying(300)",
            oldMaxLength: 300,
            oldNullable: true,
            oldCollation: "postgresIndetermanisticCollation");
    }
}
