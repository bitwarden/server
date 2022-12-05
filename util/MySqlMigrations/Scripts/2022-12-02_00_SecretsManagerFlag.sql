START TRANSACTION;

ALTER TABLE `Organization` ADD `UseSecretsManager` tinyint(1) NOT NULL DEFAULT FALSE;

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20221202104048_SecretsManagerFlag', '6.0.4');

COMMIT;
