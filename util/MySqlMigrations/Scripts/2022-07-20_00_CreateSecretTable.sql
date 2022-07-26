START TRANSACTION;

CREATE TABLE `Secret` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `OrganizationId` char(36) COLLATE ascii_general_ci NOT NULL,
    `Key` longtext CHARACTER SET utf8mb4 NULL,
    `Value` longtext CHARACTER SET utf8mb4 NULL,
    `Note` longtext CHARACTER SET utf8mb4 NULL,
    `CreationDate` datetime(6) NOT NULL,
    `RevisionDate` datetime(6) NOT NULL,
    `DeletedDate` datetime(6) NULL,
    CONSTRAINT `PK_Secret` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_Secret_Organization_OrganizationId` FOREIGN KEY (`OrganizationId`) REFERENCES `Organization` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE INDEX `IX_Secret_DeletedDate` ON `Secret` (`DeletedDate`);

CREATE INDEX `IX_Secret_OrganizationId` ON `Secret` (`OrganizationId`);

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20220720222516_CreateSecretTable', '6.0.4');

COMMIT;