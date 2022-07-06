START TRANSACTION;

ALTER TABLE `OrganizationUser` MODIFY COLUMN `Status` smallint NOT NULL;

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20220608191914_DeactivatedUserStatus', '5.0.12');

COMMIT;