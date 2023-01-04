START TRANSACTION;

CREATE TABLE `ServiceAccount` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `OrganizationId` char(36) COLLATE ascii_general_ci NOT NULL,
    `Name` longtext CHARACTER SET utf8mb4 NULL,
    `CreationDate` datetime(6) NOT NULL,
    `RevisionDate` datetime(6) NOT NULL,
    CONSTRAINT `PK_ServiceAccount` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_ServiceAccount_Organization_OrganizationId` FOREIGN KEY (`OrganizationId`) REFERENCES `Organization` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE INDEX `IX_ServiceAccount_OrganizationId` ON `ServiceAccount` (`OrganizationId`);

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20220926191322_SmServiceAccount', '6.0.4');

COMMIT;
