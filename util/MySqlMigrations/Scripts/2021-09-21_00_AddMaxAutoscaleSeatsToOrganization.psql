START TRANSACTION;

ALTER TABLE `Organization` ADD `MaxAutoscaleSeats` int NULL;

ALTER TABLE `Organization` ADD `OwnersNotifiedOfAutoscaling` datetime(6) NULL;

ALTER TABLE `Event` ADD `ProviderOrganizationId` char(36) COLLATE ascii_general_ci NULL;

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20210921132418_AddMaxAutoscaleSeatsToOrganization', '5.0.9');

COMMIT;
