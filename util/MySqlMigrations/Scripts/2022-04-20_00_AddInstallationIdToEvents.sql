START TRANSACTION;

ALTER TABLE `OrganizationSponsorship` DROP FOREIGN KEY `FK_OrganizationSponsorship_Organization_SponsoringOrganizationId`;

ALTER TABLE `OrganizationSponsorship` MODIFY COLUMN `SponsoringOrganizationId` char(36) COLLATE ascii_general_ci NULL;

ALTER TABLE `Event` ADD `InstallationId` char(36) COLLATE ascii_general_ci NULL;

ALTER TABLE `OrganizationSponsorship` ADD CONSTRAINT `FK_OrganizationSponsorship_Organization_SponsoringOrganizationId` FOREIGN KEY (`SponsoringOrganizationId`) REFERENCES `Organization` (`Id`) ON DELETE RESTRICT;

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20220420170738_AddInstallationIdToEvents', '5.0.12');

COMMIT;
