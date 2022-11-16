START TRANSACTION;

ALTER TABLE `Event` ADD `SystemUser` tinyint unsigned NULL;

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20220927142038_EventsSystemUser', '6.0.4');

COMMIT;