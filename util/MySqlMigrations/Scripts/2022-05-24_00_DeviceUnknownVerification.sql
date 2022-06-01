START TRANSACTION;

ALTER TABLE `User` ADD `UnknownDeviceVerificationEnabled` tinyint(1) NOT NULL DEFAULT 1;

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20220524171600_DeviceUnknownVerification', '5.0.12');

COMMIT;