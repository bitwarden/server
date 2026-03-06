using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.MySqlMigrations.Migrations;

/// <inheritdoc />
public partial class CreateSendControlsPolicies : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
            -- Insert for orgs that have SendOptions (with or without DisableSend)
            INSERT INTO `Policy` (`Id`, `OrganizationId`, `Type`, `Enabled`, `Data`, `CreationDate`, `RevisionDate`)
            SELECT UUID(),
                   COALESCE(ds.`OrganizationId`, so.`OrganizationId`),
                   20,
                   IF(IFNULL(ds.`Enabled`, 0) = 1 OR IFNULL(so.`Enabled`, 0) = 1, 1, 0),
                   CONCAT('{""disableSend"":',
                          IF(IFNULL(ds.`Enabled`, 0) = 1, 'true', 'false'),
                          ',""disableHideEmail"":',
                          IF(so.`Data` IS NOT NULL AND JSON_VALID(so.`Data`)
                                AND JSON_EXTRACT(so.`Data`, '$.disableHideEmail') = true,
                             'true', 'false'),
                          '}'),
                   UTC_TIMESTAMP(),
                   UTC_TIMESTAMP()
            FROM (SELECT `OrganizationId`, `Enabled`, `Data` FROM `Policy` WHERE `Type` = 7) so
            LEFT JOIN (SELECT `OrganizationId`, `Enabled` FROM `Policy` WHERE `Type` = 6) ds
              ON ds.`OrganizationId` = so.`OrganizationId`
            WHERE NOT EXISTS (
                SELECT 1 FROM `Policy` sc
                WHERE sc.`OrganizationId` = COALESCE(ds.`OrganizationId`, so.`OrganizationId`)
                  AND sc.`Type` = 20
            );

            -- Insert for orgs that have DisableSend ONLY (no SendOptions)
            INSERT INTO `Policy` (`Id`, `OrganizationId`, `Type`, `Enabled`, `Data`, `CreationDate`, `RevisionDate`)
            SELECT UUID(),
                   ds.`OrganizationId`,
                   20,
                   ds.`Enabled`,
                   CONCAT('{""disableSend"":', IF(ds.`Enabled` = 1, 'true', 'false'), ',""disableHideEmail"":false}'),
                   UTC_TIMESTAMP(),
                   UTC_TIMESTAMP()
            FROM (SELECT `OrganizationId`, `Enabled` FROM `Policy` WHERE `Type` = 6) ds
            WHERE ds.`OrganizationId` NOT IN (SELECT `OrganizationId` FROM `Policy` WHERE `Type` = 7)
              AND NOT EXISTS (
                SELECT 1 FROM `Policy` sc
                WHERE sc.`OrganizationId` = ds.`OrganizationId`
                  AND sc.`Type` = 20
            );
        ");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
            DELETE FROM `Policy` WHERE `Type` = 20;
        ");
    }
}
