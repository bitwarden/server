using Bit.Core.Utilities;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.MySqlMigrations.Migrations;

public partial class FCAccessAllCollectionGroups : Migration
{
    public const string AccessAllCollectionGroupsScript = "MySqlMigrations.HelperScripts.2024-02-16_00_AccessAllCollectionGroups.sql";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(CoreHelpers.GetEmbeddedResourceContentsAsync(AccessAllCollectionGroupsScript));
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        throw new Exception("Irreversible migration");
    }
}
