using Bit.Core.Utilities;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.SqliteMigrations.Migrations;

public partial class FCAccessAllCollectionGroups : Migration
{
    private const string _accessAllCollectionGroupsScript = "SqliteMigrations.HelperScripts.2023-12-06_00_AccessAllCollectionGroups.sql";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(CoreHelpers.GetEmbeddedResourceContentsAsync(_accessAllCollectionGroupsScript));
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        throw new Exception("Irreversible migration");
    }
}
