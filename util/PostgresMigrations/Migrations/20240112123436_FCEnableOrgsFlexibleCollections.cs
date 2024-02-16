using Bit.Core.Utilities;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.PostgresMigrations.Migrations;

public partial class FCEnableOrgsFlexibleCollections : Migration
{
    private const string _enableOrgsFlexibleCollectionsScript = "PostgresMigrations.HelperScripts.2024-02-16_03_EnableOrgsFlexibleCollections.psql";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(CoreHelpers.GetEmbeddedResourceContentsAsync(_enableOrgsFlexibleCollectionsScript));
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        throw new Exception("Irreversible migration");
    }
}
