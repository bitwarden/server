using Bit.Core.Utilities;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.PostgresMigrations.Migrations;

/// <inheritdoc />
public partial class DropPasswordHealthApplications : Migration
{
    string _dropPasswordHealthReportApplications = "PostgresMigrations.HelperScripts.2024-11-04_00_DropPasswordHealthReportApplications.psql";
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(CoreHelpers.GetEmbeddedResourceContentsAsync(_dropPasswordHealthReportApplications));
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}
