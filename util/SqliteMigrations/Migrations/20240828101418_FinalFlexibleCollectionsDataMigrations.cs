using Bit.Core.Utilities;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.SqliteMigrations.Migrations;

public partial class FinalFlexibleCollectionsDataMigrations : Migration
{
    private const string _finalFlexibleCollectionsDataMigrationsScript = "SqliteMigrations.HelperScripts.2024-08-26_00_FinalFlexibleCollectionsDataMigrations.sql";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(CoreHelpers.GetEmbeddedResourceContentsAsync(_finalFlexibleCollectionsDataMigrationsScript));
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        throw new Exception("Irreversible migration");
    }
}
