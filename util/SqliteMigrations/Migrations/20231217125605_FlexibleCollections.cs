using Bit.Core.Utilities;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.SqliteMigrations.Migrations;

public partial class FlexibleCollections : Migration
{
    private const string _accessAllCollectionGroupsScript = "SqliteMigrations.HelperScripts.2023-12-06_00_AccessAllCollectionGroups.sql";
    private const string _accessAllCollectionUsersScript = "SqliteMigrations.HelperScripts.2023-12-06_01_AccessAllCollectionUsers.sql";
    private const string _managersEditAssignedCollectionUsersScript = "SqliteMigrations.HelperScripts.2023-12-06_02_ManagersEditAssignedCollectionUsers.sql";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(CoreHelpers.GetEmbeddedResourceContentsAsync(_accessAllCollectionGroupsScript));
        migrationBuilder.Sql(CoreHelpers.GetEmbeddedResourceContentsAsync(_accessAllCollectionUsersScript));
        migrationBuilder.Sql(CoreHelpers.GetEmbeddedResourceContentsAsync(_managersEditAssignedCollectionUsersScript));
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        throw new Exception("Irreversible migration");
    }
}
