using Bit.Core.Utilities;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.MySqlMigrations.Migrations;

/// <inheritdoc />
public partial class AddUseOrganizationDomains : Migration
{
    private const string _addUseOrganizationDomainsMigrationScript = "MySqlMigrations.HelperScripts.2025-04-23_00_AddUseOrganizationDomains.sql";
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "UseOrganizationDomains",
            table: "Organization",
            type: "tinyint(1)",
            nullable: false,
            defaultValue: false);

        migrationBuilder.Sql(CoreHelpers.GetEmbeddedResourceContentsAsync(_addUseOrganizationDomainsMigrationScript));
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        throw new Exception("Irreversible migration");
    }
}
