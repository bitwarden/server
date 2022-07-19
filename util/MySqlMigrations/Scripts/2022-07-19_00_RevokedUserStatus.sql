START TRANSACTION;

ALTER TABLE `OrganizationUser` MODIFY COLUMN `Status` smallint NOT NULL;

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20220719191914_RevokedUserStatus', '5.0.12');

COMMIT;