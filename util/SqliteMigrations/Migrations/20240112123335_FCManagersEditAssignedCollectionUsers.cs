using Bit.Core.Utilities;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.SqliteMigrations.Migrations;

public partial class FCManagersEditAssignedCollectionUsers : Migration
{
    private const string _managersEditAssignedCollectionUsersScript = "SqliteMigrations.HelperScripts.2024-01-12_02_ManagersEditAssignedCollectionUsers.sql";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(CoreHelpers.GetEmbeddedResourceContentsAsync(_managersEditAssignedCollectionUsersScript));
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        throw new Exception("Irreversible migration");
    }
}
