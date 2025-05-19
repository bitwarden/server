using Bit.Core.Utilities;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.PostgresMigrations.Migrations;

/// <inheritdoc />
public partial class AddUseOrganizationDomainsData : Migration
{

    private const string _addUseOrganizationDomainsMigrationScript = "PostgresMigrations.HelperScripts.2025-05-13_00_AddUseOrganizationDomains.psql";

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(CoreHelpers.GetEmbeddedResourceContentsAsync(_addUseOrganizationDomainsMigrationScript));
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        throw new Exception("Irreversible migration.");
    }
}
