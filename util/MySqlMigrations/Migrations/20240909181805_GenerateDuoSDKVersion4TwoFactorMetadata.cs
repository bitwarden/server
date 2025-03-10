using Bit.Core.Utilities;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.MySqlMigrations.Migrations;

/// <inheritdoc />
public partial class GenerateDuoSDKVersion4TwoFactorMetadata : Migration
{
    private const string _duoTwoFactorDataMigrationsScript = "MySqlMigrations.HelperScripts.2024-09-05_00_SyncDuoVersionFourMetadataToVersionTwo.sql";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(CoreHelpers.GetEmbeddedResourceContentsAsync(_duoTwoFactorDataMigrationsScript));
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        throw new Exception("Irreversible migration");
    }
}
