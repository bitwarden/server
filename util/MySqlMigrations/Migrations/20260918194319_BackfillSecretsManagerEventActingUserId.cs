using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.MySqlMigrations.Migrations;

/// <inheritdoc />
public partial class BackfillSecretsManagerEventActingUserId : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Secret (2100-2199) and project (2200-2299) events recorded the human actor in UserId
        // and left ActingUserId NULL, so the public events API reported no actor for them.
        // ServiceAccount_* (2300-2399) are excluded and must stay excluded: they already set
        // ActingUserId, and their UserId column holds an OrganizationUser id, not a user id.
        // ActingUserId IS NULL skips rows written after the fix; UserId IS NOT NULL skips
        // machine-account rows, which record a ServiceAccountId and no user.
        migrationBuilder.Sql(@"
                UPDATE `Event`
                SET `ActingUserId` = `UserId`
                WHERE ((`Type` >= 2100 AND `Type` <= 2199) OR (`Type` >= 2200 AND `Type` <= 2299))
                  AND `ActingUserId` IS NULL
                  AND `UserId` IS NOT NULL;
            ");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}
