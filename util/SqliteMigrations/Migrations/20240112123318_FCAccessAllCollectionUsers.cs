using Bit.Core.Utilities;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.SqliteMigrations.Migrations;

public partial class FCAccessAllCollectionUsers : Migration
{
    private const string _accessAllCollectionUsersScript = "SqliteMigrations.HelperScripts.2024-01-12_01_AccessAllCollectionUsers.sql";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(CoreHelpers.GetEmbeddedResourceContentsAsync(_accessAllCollectionUsersScript));
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        throw new Exception("Irreversible migration");
    }
}
