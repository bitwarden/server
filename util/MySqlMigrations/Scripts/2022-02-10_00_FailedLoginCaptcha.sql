START TRANSACTION;

ALTER TABLE `User` ADD `FailedLoginCount` int NOT NULL DEFAULT 0;

ALTER TABLE `User` ADD `LastFailedLoginDate` datetime(6) NULL;

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20220301215315_FailedLoginCaptcha', '5.0.12');

COMMIT;