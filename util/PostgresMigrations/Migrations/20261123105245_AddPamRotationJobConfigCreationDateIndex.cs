using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.PostgresMigrations.Migrations;

/// <inheritdoc />
public partial class AddPamRotationJobConfigCreationDateIndex : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "IX_PamRotationJob_RotationConfigId_CreationDate_Id",
            table: "PamRotationJob",
            columns: new[] { "RotationConfigId", "CreationDate", "Id" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_PamRotationJob_RotationConfigId_CreationDate_Id",
            table: "PamRotationJob");
    }
}
