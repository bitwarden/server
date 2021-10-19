START TRANSACTION;

DROP TABLE `ProviderOrganizationProviderUser`;

ALTER TABLE `Provider` ADD `UseEvents` tinyint(1) NOT NULL DEFAULT FALSE;

ALTER TABLE `Event` ADD `ProviderId` char(36) COLLATE ascii_general_ci NULL;

ALTER TABLE `Event` ADD `ProviderUserId` char(36) COLLATE ascii_general_ci NULL;

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20210709095522_RemoveProviderOrganizationProviderUser', '5.0.5');

COMMIT;
