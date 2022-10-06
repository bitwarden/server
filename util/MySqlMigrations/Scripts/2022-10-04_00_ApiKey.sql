START TRANSACTION;

CREATE TABLE `ApiKey` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `UserId` char(36) COLLATE ascii_general_ci NULL,
    `OrganizationId` char(36) COLLATE ascii_general_ci NULL,
    `ServiceAccountId` char(36) COLLATE ascii_general_ci NULL,
    `Name` varchar(200) CHARACTER SET utf8mb4 NULL,
    `ClientSecret` varchar(30) CHARACTER SET utf8mb4 NULL,
    `Scope` varchar(4000) CHARACTER SET utf8mb4 NULL,
    `EncryptedPayload` varchar(4000) CHARACTER SET utf8mb4 NULL,
    `ExpireAt` datetime(6) NOT NULL,
    `CreationDate` datetime(6) NOT NULL,
    `RevisionDate` datetime(6) NOT NULL,
    CONSTRAINT `PK_ApiKey` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_ApiKey_Organization_OrganizationId` FOREIGN KEY (`OrganizationId`) REFERENCES `Organization` (`Id`),
    CONSTRAINT `FK_ApiKey_ServiceAccount_ServiceAccountId` FOREIGN KEY (`ServiceAccountId`) REFERENCES `ServiceAccount` (`Id`),
    CONSTRAINT `FK_ApiKey_User_UserId` FOREIGN KEY (`UserId`) REFERENCES `User` (`Id`)
) CHARACTER SET=utf8mb4;

CREATE INDEX `IX_ApiKey_OrganizationId` ON `ApiKey` (`OrganizationId`);

CREATE INDEX `IX_ApiKey_ServiceAccountId` ON `ApiKey` (`ServiceAccountId`);

CREATE INDEX `IX_ApiKey_UserId` ON `ApiKey` (`UserId`);

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20221004175950_ApiKey', '6.0.4');

COMMIT;
