using Bit.Core.Utilities;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.MySqlMigrations.Migrations;

public partial class FCEnableOrgsFlexibleCollections : Migration
{
    private const string _enableOrgsFlexibleCollectionsScript = "MySqlMigrations.HelperScripts.2024-02-16_03_EnableOrgsFlexibleCollections.sql";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(CoreHelpers.GetEmbeddedResourceContentsAsync(_enableOrgsFlexibleCollectionsScript));
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        throw new Exception("Irreversible migration");
    }
}
