using System;
using Bit.Core.Utilities;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.PostgresMigrations.Migrations
{
    public partial class FlexibleCollections : Migration
    {
        private const string _accessAllCollectionGroupsScript = "PostgresMigrations.HelperScripts.2023-12-06_00_AccessAllCollectionGroups.psql";
        private const string _accessAllCollectionUsersScript = "PostgresMigrations.HelperScripts.2023-12-06_01_AccessAllCollectionUsers.psql";
        private const string _managersEditAssignedCollectionUsersScript = "PostgresMigrations.HelperScripts.2023-12-06_02_ManagersEditAssignedCollectionUsers.psql";

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
}
