using Bit.Core.Utilities;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.SqliteMigrations.Migrations;

public partial class EnableOrgsCollectionEnhancements : Migration
{
    private const string _enableOrgsCollectionEnhancementsScript = "SqliteMigrations.HelperScripts.2024-04-25_00_EnableOrgsCollectionEnhancements.sql";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(CoreHelpers.GetEmbeddedResourceContentsAsync(_enableOrgsCollectionEnhancementsScript));
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        throw new Exception("Irreversible migration");
    }
}
