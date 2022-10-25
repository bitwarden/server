START TRANSACTION;

ALTER TABLE `AuthRequest` ADD `Approved` tinyint(1) NULL;

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20221024210500_PasswordlessAuthRequestAddApprovedColumn', '6.0.4');

COMMIT;