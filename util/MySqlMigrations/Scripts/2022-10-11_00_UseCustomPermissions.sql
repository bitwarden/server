START TRANSACTION;

ALTER TABLE `Organization` ADD `UseCustomPermissions` tinyint(1) NOT NULL DEFAULT FALSE;

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20221020154432_UseCustomPermissionsFlag', '6.0.4');

COMMIT;
