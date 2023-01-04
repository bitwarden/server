START TRANSACTION;

CREATE TABLE `Project` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `OrganizationId` char(36) COLLATE ascii_general_ci NOT NULL,
    `Name` longtext CHARACTER SET utf8mb4 NULL,
    `CreationDate` datetime(6) NOT NULL,
    `RevisionDate` datetime(6) NOT NULL,
    `DeletedDate` datetime(6) NULL,
    CONSTRAINT `PK_Project` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_Project_Organization_OrganizationId` FOREIGN KEY (`OrganizationId`) REFERENCES `Organization` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE INDEX `IX_Project_DeletedDate` ON `Project` (`DeletedDate`);

CREATE INDEX `IX_Project_OrganizationId` ON `Project` (`OrganizationId`);

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20220830195738_CreateProjectTable', '6.0.4');

COMMIT;
