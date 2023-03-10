START TRANSACTION;

ALTER TABLE `OrganizationDomain` ADD `LastCheckedDate` datetime(6) NULL;

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20221209192355_OrganizationDomainLastCheckedDate', '6.0.4');

COMMIT;

