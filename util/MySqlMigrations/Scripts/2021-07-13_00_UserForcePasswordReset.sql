START TRANSACTION;

ALTER TABLE `User` ADD `ForcePasswordReset` tinyint(1) NOT NULL DEFAULT FALSE;

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20210716142145_UserForcePasswordReset', '5.0.5');

COMMIT;