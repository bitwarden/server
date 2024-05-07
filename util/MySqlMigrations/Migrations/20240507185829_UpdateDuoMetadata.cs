using Bit.Core.Utilities;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.MySqlMigrations.Migrations;

/// <inheritdoc />
public partial class UpdateDuoMetadata : Migration
{
    private const string _updateDuoMetadata = "MySqlMigrations.HelperScripts.2024-05-03_00_UpdateDuoMetadata.sql";
    private const string _revertUpdateDuoMetadata = "MySqlMigrations.HelperScripts.2024-05-06_00_RevertUpdateDuoMetadata.sql";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(CoreHelpers.GetEmbeddedResourceContentsAsync(_updateDuoMetadata));
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(CoreHelpers.GetEmbeddedResourceContentsAsync(_revertUpdateDuoMetadata));
    }
}
