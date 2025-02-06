using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.MySqlMigrations.Migrations;

/// <inheritdoc />
public partial class SyncOrganizationLimitCollectionCreationDeletionColumn : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
        @"
                    UPDATE Organization
                    SET
                      LimitCollectionCreation = LimitCollectionCreationDeletion,
                      LimitCollectionDeletion = LimitCollectionCreationDeletion;
                ");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {

    }
}
