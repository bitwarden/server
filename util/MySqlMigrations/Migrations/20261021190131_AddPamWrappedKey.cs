using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.MySqlMigrations.Migrations;

/// <inheritdoc />
public partial class AddPamWrappedKey : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "WrappedKey",
            table: "PamTargetSystem",
            type: "varchar(2000)",
            maxLength: 2000,
            nullable: true)
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.AddColumn<string>(
            name: "WrappedKey",
            table: "PamRotationConfig",
            type: "varchar(2000)",
            maxLength: 2000,
            nullable: true)
            .Annotation("MySql:CharSet", "utf8mb4");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "WrappedKey",
            table: "PamTargetSystem");

        migrationBuilder.DropColumn(
            name: "WrappedKey",
            table: "PamRotationConfig");
    }
}
