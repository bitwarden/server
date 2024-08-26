using Bit.Core.Utilities;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.PostgresMigrations.Migrations;

public partial class FinalFlexibleCollectionsDataMigrations : Migration
{
    private const string _finalFlexibleCollectionsDataMigrationsScript = "PostgresMigrations.HelperScripts.2024-08-26_00_FinalFlexibleCollectionsDataMigrations.psql";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(CoreHelpers.GetEmbeddedResourceContentsAsync(_finalFlexibleCollectionsDataMigrationsScript));
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        throw new Exception("Irreversible migration");
    }
}
