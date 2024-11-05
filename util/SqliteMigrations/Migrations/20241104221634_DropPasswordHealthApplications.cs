using Bit.Core.Utilities;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.SqliteMigrations.Migrations;

/// <inheritdoc />
public partial class DropPasswordHealthApplications : Migration
{
    /// <inheritdoc />

    string _dropPasswordHealthReportApplications = "SqliteMigrations.HelperScripts.2024-11-04_00_DropPasswordHealthReportApplications.sql";
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(CoreHelpers.GetEmbeddedResourceContentsAsync(_dropPasswordHealthReportApplications));
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}
