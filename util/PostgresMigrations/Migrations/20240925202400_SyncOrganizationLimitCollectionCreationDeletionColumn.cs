using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.PostgresMigrations.Migrations;

/// <inheritdoc />
public partial class SyncOrganizationLimitCollectionCreationDeletionColumn : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Postgres is particular about the casing of entities. It wants to
        // lowercase everything by default, and convert casings
        // automatically. Quoting the entity names here provides explicit &
        // correct casing.
        migrationBuilder.Sql(
        @"
                UPDATE ""Organization""
                SET
                  ""LimitCollectionCreation"" = ""LimitCollectionCreationDeletion"",
                  ""LimitCollectionDeletion"" = ""LimitCollectionCreationDeletion"";
            ");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {

    }
}
