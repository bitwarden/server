START TRANSACTION;

CREATE TABLE `OrganizationDomain` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `OrganizationId` char(36) COLLATE ascii_general_ci NOT NULL,
    `Txt` longtext CHARACTER SET utf8mb4 NULL,
    `DomainName` varchar(255) CHARACTER SET utf8mb4 NULL,
    `CreationDate` datetime(6) NOT NULL,
    `VerifiedDate` datetime(6) NULL,
    `NextRunDate` datetime(6) NOT NULL,
    `NextRunCount` int NOT NULL,
    CONSTRAINT `PK_OrganizationDomain` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_OrganizationDomain_Organization_OrganizationId` FOREIGN KEY (`OrganizationId`) REFERENCES `Organization` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE INDEX `IX_OrganizationDomain_OrganizationId` ON `OrganizationDomain` (`OrganizationId`);

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20221108015516_OrganizationDomainClaim', '6.0.4');

COMMIT;