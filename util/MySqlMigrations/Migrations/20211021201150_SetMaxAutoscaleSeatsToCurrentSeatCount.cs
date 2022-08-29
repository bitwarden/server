using Bit.Core.Utilities;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Bit.MySqlMigrations.Migrations;

public partial class SetMaxAutoscaleSeatsToCurrentSeatCount : Migration
{
    private const string _scriptLocation =
        "MySqlMigrations.Scripts.2021-10-21_00_SetMaxAutoscaleSeatCount.sql";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(CoreHelpers.GetEmbeddedResourceContentsAsync(_scriptLocation));
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        throw new Exception("Irreversible migration");
    }
}
