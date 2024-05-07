using Bit.Core.Utilities;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.PostgresMigrations.Migrations;

/// <inheritdoc />
public partial class UpdateDuoMetadata : Migration
{
    private const string _updateDuoMetadata = "PostgresMigrations.HelperScripts.2024-05-03_00_UpdateDuoMetadata.psql";
    private const string _revertUpdateDuoMetadata = "PostgresMigrations.HelperScripts.2024-05-06_00_RevertUpdateDuoMetadata.psql";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(CoreHelpers.GetEmbeddedResourceContentsAsync(_updateDuoMetadata));
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(CoreHelpers.GetEmbeddedResourceContentsAsync(_revertUpdateDuoMetadata));
    }
}
