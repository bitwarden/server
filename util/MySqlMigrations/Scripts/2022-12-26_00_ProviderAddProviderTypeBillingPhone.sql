START TRANSACTION;

ALTER TABLE `Provider` ADD `BillingPhone` longtext CHARACTER SET utf8mb4 NULL;

ALTER TABLE `Provider` ADD `Type` tinyint unsigned NOT NULL DEFAULT 0;

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20221226164641_ProviderAddProviderTypeBillingPhone', '6.0.12');

COMMIT;