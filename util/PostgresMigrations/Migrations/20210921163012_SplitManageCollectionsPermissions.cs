using System;
using Bit.Core.Utilities;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Bit.PostgresMigrations.Migrations
{
    public partial class SplitManageCollectionsPermissions : Migration
    {
        private const string _scriptLocation =
            "PostgresMigration.Scripts.2021-09-21_00_SplitManageCollectionsPermission.psql";

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
