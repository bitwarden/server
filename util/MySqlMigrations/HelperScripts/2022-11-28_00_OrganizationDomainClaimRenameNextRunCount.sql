START TRANSACTION;

ALTER TABLE `OrganizationDomain` RENAME COLUMN `NextRunCount` TO `JobRunCount`;

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20221129004644_OrganizationDomainClaimRenameNextRunCount', '6.0.4');

COMMIT;