using Bit.Core.Utilities;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.PostgresMigrations.Migrations;

public partial class FCAccessAllCollectionGroups : Migration
{
    private const string _accessAllCollectionGroupsScript = "PostgresMigrations.HelperScripts.2024-02-16_00_AccessAllCollectionGroups.psql";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(CoreHelpers.GetEmbeddedResourceContentsAsync(_accessAllCollectionGroupsScript));
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        throw new Exception("Irreversible migration");
    }
}
