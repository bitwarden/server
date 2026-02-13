using Bit.Core.Utilities;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.PostgresMigrations.Migrations;

/// <inheritdoc />
public partial class HydrateUserMasterPasswordSalt : Migration
{
    private const string _hydrateScript = "PostgresMigrations.HelperScripts.2026-02-11_00_HydrateUserMasterPasswordSalt.psql";

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
