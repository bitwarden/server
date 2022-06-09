START TRANSACTION;

ALTER TABLE `OrganizationSponsorship` DROP FOREIGN KEY `FK_OrganizationSponsorship_Organization_SponsoringOrganizationId`;

ALTER TABLE `OrganizationSponsorship` MODIFY COLUMN `SponsoringOrganizationUserId` char(36) COLLATE ascii_general_ci NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';

ALTER TABLE `OrganizationSponsorship` MODIFY COLUMN `SponsoringOrganizationId` char(36) COLLATE ascii_general_ci NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';

ALTER TABLE `OrganizationSponsorship` ADD CONSTRAINT `FK_OrganizationSponsorship_Organization_SponsoringOrganizationId` FOREIGN KEY (`SponsoringOrganizationId`) REFERENCES `Organization` (`Id`) ON DELETE CASCADE;

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20220411191518_SponsorshipBulkActions', '5.0.12');

COMMIT;