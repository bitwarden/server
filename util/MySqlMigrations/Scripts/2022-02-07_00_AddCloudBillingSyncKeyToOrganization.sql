START TRANSACTION;

ALTER TABLE `Organization` ADD `CloudBillingSyncKey` varchar(30) CHARACTER SET utf8mb4 NULL;

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20220207221514_AddCloudBillingSyncKeyToOrganization', '5.0.12');

COMMIT;
