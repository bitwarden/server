using Bit.Core.Utilities;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.SqliteMigrations.Migrations;

/// <inheritdoc />
public partial class HydrateUserMasterPasswordSalt : Migration
{
    private const string _hydrateScript = "SqliteMigrations.HelperScripts.2026-02-11_00_HydrateUserMasterPasswordSalt.sql";

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(CoreHelpers.GetEmbeddedResourceContentsAsync(_hydrateScript));
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        throw new Exception("Irreversible migration");
    }
}
