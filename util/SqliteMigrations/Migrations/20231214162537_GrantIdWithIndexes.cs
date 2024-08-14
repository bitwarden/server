using Bit.EfShared;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.SqliteMigrations.Migrations;

/// <inheritdoc />
public partial class GrantIdWithIndexes : Migration
{
    private const string _scriptLocationTemplate = "2023-12-04_00_{0}_GrantIndexes.sql";

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.SqlResource(_scriptLocationTemplate);

        migrationBuilder.CreateIndex(
            name: "IX_Grant_Key",
            table: "Grant",
            column: "Key",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.SqlResource(_scriptLocationTemplate);

        migrationBuilder.DropIndex(
            name: "IX_Grant_Key",
            table: "Grant");
    }
}
