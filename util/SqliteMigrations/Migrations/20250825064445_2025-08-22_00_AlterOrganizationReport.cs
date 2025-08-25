﻿using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.SqliteMigrations.Migrations;

/// <inheritdoc />
public partial class _20250822_00_AlterOrganizationReport : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.RenameColumn(
            name: "Date",
            table: "OrganizationReport",
            newName: "RevisionDate");

        migrationBuilder.AddColumn<string>(
            name: "ApplicationData",
            table: "OrganizationReport",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "SummaryData",
            table: "OrganizationReport",
            type: "TEXT",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "ApplicationData",
            table: "OrganizationReport");

        migrationBuilder.DropColumn(
            name: "SummaryData",
            table: "OrganizationReport");

        migrationBuilder.RenameColumn(
            name: "RevisionDate",
            table: "OrganizationReport",
            newName: "Date");
    }
}
