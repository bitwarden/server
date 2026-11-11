using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.SqliteMigrations.Migrations;

/// <inheritdoc />
public partial class AddPamWrappedKey : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "WrappedKey",
            table: "PamTargetSystem",
            type: "TEXT",
            maxLength: 2000,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "WrappedKey",
            table: "PamRotationConfig",
            type: "TEXT",
            maxLength: 2000,
            nullable: true);
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
