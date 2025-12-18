START TRANSACTION;

ALTER TABLE `Event` ADD `DomainName` longtext CHARACTER SET utf8mb4 NULL;

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20221209015017_EventsDomainName', '6.0.4');

COMMIT;

