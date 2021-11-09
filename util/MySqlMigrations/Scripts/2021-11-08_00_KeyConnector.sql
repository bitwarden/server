START TRANSACTION;

ALTER TABLE `User` ADD `UsesKeyConnector` tinyint(1) NOT NULL DEFAULT FALSE;

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20211108041911_KeyConnector', '5.0.9');

COMMIT;

