﻿using Bit.Core.Utilities;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.MySqlMigrations.Migrations;

/// <inheritdoc />
public partial class DropPasswordHealthApplications : Migration
{
    string dropPasswordHealtReportApplicationsTable = "MySqlMigrations.HelperScripts.2024-11-04-00_DropPasswordHealthReportApplications.sql";
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(CoreHelpers.GetEmbeddedResourceContentsAsync(dropPasswordHealtReportApplicationsTable));
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}
