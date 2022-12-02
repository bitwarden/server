START TRANSACTION;

ALTER TABLE `ApiKey` MODIFY COLUMN `ExpireAt` datetime(6) NULL;

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20221130233348_NeverExpireApiKey', '6.0.4');

COMMIT;
