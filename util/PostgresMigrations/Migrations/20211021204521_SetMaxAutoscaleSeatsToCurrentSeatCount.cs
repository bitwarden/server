using System;
using Bit.Core.Utilities;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Bit.PostgresMigrations.Migrations
{
    public partial class SetMaxAutoscaleSeatsToCurrentSeatCount : Migration
    {
        private const string _scriptLocation =
            "PostgresMigration.Scripts.2021-10-21_00_SetMaxAutoscaleSeatCount.sql";

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(CoreHelpers.GetEmbeddedResourceContentsAsync(_scriptLocation));
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new Exception("Irreversible migration");
        }
    }
}
