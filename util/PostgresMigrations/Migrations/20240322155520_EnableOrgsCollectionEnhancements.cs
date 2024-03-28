using Bit.Core.Utilities;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.PostgresMigrations.Migrations;

public partial class EnableOrgsCollectionEnhancements : Migration
{
    private const string _enableOrgsCollectionEnhancementsScript = "PostgresMigrations.HelperScripts.2024-03-22_00_EnableOrgsCollectionEnhancements.psql";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(CoreHelpers.GetEmbeddedResourceContentsAsync(_enableOrgsCollectionEnhancementsScript));
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        throw new Exception("Irreversible migration");
    }
}
