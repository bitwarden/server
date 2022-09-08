START TRANSACTION;

ALTER TABLE `OrganizationUser` MODIFY COLUMN `Status` smallint NOT NULL;

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20220608191914_DeactivatedUserStatus', '6.0.4');

COMMIT;

START TRANSACTION;

ALTER TABLE `Organization` ADD `UseScim` tinyint(1) NOT NULL DEFAULT FALSE;

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20220707163017_UseScimFlag', '6.0.4');

COMMIT;