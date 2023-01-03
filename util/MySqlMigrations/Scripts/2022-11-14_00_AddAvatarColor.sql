START TRANSACTION;

ALTER TABLE `User` ADD `AvatarColor` varchar(7) CHARACTER SET utf8mb4 NULL;

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20221115030843_AvatarColor', '6.0.4');

COMMIT;

