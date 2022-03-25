START TRANSACTION;

ALTER TABLE `OrganizationSponsorship` DROP FOREIGN KEY `FK_OrganizationSponsorship_Installation_InstallationId`;

ALTER TABLE `OrganizationSponsorship` DROP INDEX `IX_OrganizationSponsorship_InstallationId`;

ALTER TABLE `OrganizationSponsorship` DROP COLUMN `InstallationId`;

ALTER TABLE `OrganizationSponsorship` DROP COLUMN `TimesRenewedWithoutValidation`;

CREATE TABLE `OrganizationApiKey` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `OrganizationId` char(36) COLLATE ascii_general_ci NOT NULL,
    `Type` tinyint unsigned NOT NULL,
    `ApiKey` varchar(30) CHARACTER SET utf8mb4 NULL,
    `RevisionDate` datetime(6) NOT NULL,
    CONSTRAINT `PK_OrganizationApiKey` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_OrganizationApiKey_Organization_OrganizationId` FOREIGN KEY (`OrganizationId`) REFERENCES `Organization` (`Id`) ON DELETE CASCADE
) CHARACTER SET utf8mb4;

INSERT INTO OrganizationApiKey(Id, OrganizationId, Type, ApiKey, RevisionDate)
SELECT UUID(), Id, 0, ApiKey, RevisionDate
FROM Organization;


ALTER TABLE `Organization` DROP COLUMN `ApiKey`;

ALTER TABLE `OrganizationSponsorship` RENAME COLUMN `SponsorshipLapsedDate` TO `ValidUntil`;

ALTER TABLE `OrganizationSponsorship` RENAME COLUMN `CloudSponsor` TO `ToDelete`;

CREATE TABLE `OrganizationConnection` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `Type` tinyint unsigned NOT NULL,
    `OrganizationId` char(36) COLLATE ascii_general_ci NOT NULL,
    `Enabled` tinyint(1) NOT NULL,
    `Config` longtext CHARACTER SET utf8mb4 NULL,
    CONSTRAINT `PK_OrganizationConnection` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_OrganizationConnection_Organization_OrganizationId` FOREIGN KEY (`OrganizationId`) REFERENCES `Organization` (`Id`) ON DELETE CASCADE
) CHARACTER SET utf8mb4;

CREATE INDEX `IX_OrganizationApiKey_OrganizationId` ON `OrganizationApiKey` (`OrganizationId`);

CREATE INDEX `IX_OrganizationConnection_OrganizationId` ON `OrganizationConnection` (`OrganizationId`);

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20220322191314_SelfHostF4E', '5.0.12');

COMMIT;

