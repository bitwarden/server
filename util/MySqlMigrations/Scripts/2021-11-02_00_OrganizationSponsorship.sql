START TRANSACTION;

ALTER TABLE `User` ADD `UsesCryptoAgent` tinyint(1) NOT NULL DEFAULT FALSE;

CREATE TABLE `OrganizationSponsorship` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `InstallationId` char(36) COLLATE ascii_general_ci NULL,
    `SponsoringOrganizationId` char(36) COLLATE ascii_general_ci NULL,
    `SponsoringOrganizationUserId` char(36) COLLATE ascii_general_ci NULL,
    `SponsoredOrganizationId` char(36) COLLATE ascii_general_ci NULL,
    `FriendlyName` varchar(256) CHARACTER SET utf8mb4 NULL,
    `OfferedToEmail` varchar(256) CHARACTER SET utf8mb4 NULL,
    `PlanSponsorshipType` tinyint unsigned NULL,
    `CloudSponsor` tinyint(1) NOT NULL,
    `LastSyncDate` datetime(6) NULL,
    `TimesRenewedWithoutValidation` tinyint unsigned NOT NULL,
    `SponsorshipLapsedDate` datetime(6) NULL,
    CONSTRAINT `PK_OrganizationSponsorship` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_OrganizationSponsorship_Installation_InstallationId` FOREIGN KEY (`InstallationId`) REFERENCES `Installation` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_OrganizationSponsorship_Organization_SponsoredOrganizationId` FOREIGN KEY (`SponsoredOrganizationId`) REFERENCES `Organization` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_OrganizationSponsorship_Organization_SponsoringOrganizationId` FOREIGN KEY (`SponsoringOrganizationId`) REFERENCES `Organization` (`Id`) ON DELETE RESTRICT
) CHARACTER SET utf8mb4;

CREATE INDEX `IX_OrganizationSponsorship_InstallationId` ON `OrganizationSponsorship` (`InstallationId`);

CREATE INDEX `IX_OrganizationSponsorship_SponsoredOrganizationId` ON `OrganizationSponsorship` (`SponsoredOrganizationId`);

CREATE INDEX `IX_OrganizationSponsorship_SponsoringOrganizationId` ON `OrganizationSponsorship` (`SponsoringOrganizationId`);

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20211108225243_OrganizationSponsorship', '5.0.9');

COMMIT;
