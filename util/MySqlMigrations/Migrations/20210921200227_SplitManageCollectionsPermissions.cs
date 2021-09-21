using System;
using Bit.Core.Utilities;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Bit.MySqlMigrations.Migrations
{
    public partial class SplitManageCollectionsPermissions : Migration
    {
        private const string _scriptLocation =
            "MySqlMigrations.Scripts.2021-09-21_00_SplitManageCollectionsPermission.sql";

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(CoreHelpers.GetEmbeddedSqlAsync(_scriptLocation));
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new Exception("Irreversible migration");
        }
    }
}
