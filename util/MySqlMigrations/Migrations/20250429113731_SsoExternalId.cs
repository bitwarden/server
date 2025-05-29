using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.MySqlMigrations.Migrations;

/// <inheritdoc />
public partial class SsoExternalId : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
            name: "ExternalId",
            table: "SsoUser",
            type: "varchar(300)",
            maxLength: 300,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "varchar(50)",
            oldMaxLength: 50,
            oldNullable: true)
            .Annotation("MySql:CharSet", "utf8mb4")
            .OldAnnotation("MySql:CharSet", "utf8mb4");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
            name: "ExternalId",
            table: "SsoUser",
            type: "varchar(50)",
            maxLength: 50,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "varchar(300)",
            oldMaxLength: 300,
            oldNullable: true)
            .Annotation("MySql:CharSet", "utf8mb4")
            .OldAnnotation("MySql:CharSet", "utf8mb4");
    }
}
