START TRANSACTION;

ALTER TABLE `Organization` ADD `UseSecretsManager` tinyint(1) NOT NULL DEFAULT FALSE;

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

CREATE TABLE `ServiceAccount` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `OrganizationId` char(36) COLLATE ascii_general_ci NOT NULL,
    `Name` longtext CHARACTER SET utf8mb4 NULL,
    `CreationDate` datetime(6) NOT NULL,
    `RevisionDate` datetime(6) NOT NULL,
    CONSTRAINT `PK_ServiceAccount` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_ServiceAccount_Organization_OrganizationId` FOREIGN KEY (`OrganizationId`) REFERENCES `Organization` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `ProjectSecret` (
    `ProjectsId` char(36) COLLATE ascii_general_ci NOT NULL,
    `SecretsId` char(36) COLLATE ascii_general_ci NOT NULL,
    CONSTRAINT `PK_ProjectSecret` PRIMARY KEY (`ProjectsId`, `SecretsId`),
    CONSTRAINT `FK_ProjectSecret_Project_ProjectsId` FOREIGN KEY (`ProjectsId`) REFERENCES `Project` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_ProjectSecret_Secret_SecretsId` FOREIGN KEY (`SecretsId`) REFERENCES `Secret` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `AccessPolicy` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `GroupId` char(36) COLLATE ascii_general_ci NULL,
    `GrantedProjectId` char(36) COLLATE ascii_general_ci NULL,
    `GrantedServiceAccountId` char(36) COLLATE ascii_general_ci NULL,
    `ServiceAccountId` char(36) COLLATE ascii_general_ci NULL,
    `OrganizationUserId` char(36) COLLATE ascii_general_ci NULL,
    `Read` tinyint(1) NOT NULL,
    `Write` tinyint(1) NOT NULL,
    `CreationDate` datetime(6) NOT NULL,
    `RevisionDate` datetime(6) NOT NULL,
    `Discriminator` longtext CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK_AccessPolicy` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_AccessPolicy_Group_GroupId` FOREIGN KEY (`GroupId`) REFERENCES `Group` (`Id`),
    CONSTRAINT `FK_AccessPolicy_OrganizationUser_OrganizationUserId` FOREIGN KEY (`OrganizationUserId`) REFERENCES `OrganizationUser` (`Id`),
    CONSTRAINT `FK_AccessPolicy_Project_GrantedProjectId` FOREIGN KEY (`GrantedProjectId`) REFERENCES `Project` (`Id`),
    CONSTRAINT `FK_AccessPolicy_ServiceAccount_GrantedServiceAccountId` FOREIGN KEY (`GrantedServiceAccountId`) REFERENCES `ServiceAccount` (`Id`),
    CONSTRAINT `FK_AccessPolicy_ServiceAccount_ServiceAccountId` FOREIGN KEY (`ServiceAccountId`) REFERENCES `ServiceAccount` (`Id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `ApiKey` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `ServiceAccountId` char(36) COLLATE ascii_general_ci NULL,
    `Name` varchar(200) CHARACTER SET utf8mb4 NULL,
    `ClientSecret` varchar(30) CHARACTER SET utf8mb4 NULL,
    `Scope` varchar(4000) CHARACTER SET utf8mb4 NULL,
    `EncryptedPayload` varchar(4000) CHARACTER SET utf8mb4 NULL,
    `Key` longtext CHARACTER SET utf8mb4 NULL,
    `ExpireAt` datetime(6) NULL,
    `CreationDate` datetime(6) NOT NULL,
    `RevisionDate` datetime(6) NOT NULL,
    CONSTRAINT `PK_ApiKey` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_ApiKey_ServiceAccount_ServiceAccountId` FOREIGN KEY (`ServiceAccountId`) REFERENCES `ServiceAccount` (`Id`)
) CHARACTER SET=utf8mb4;

CREATE INDEX `IX_AccessPolicy_GrantedProjectId` ON `AccessPolicy` (`GrantedProjectId`);

CREATE INDEX `IX_AccessPolicy_GrantedServiceAccountId` ON `AccessPolicy` (`GrantedServiceAccountId`);

CREATE INDEX `IX_AccessPolicy_GroupId` ON `AccessPolicy` (`GroupId`);

CREATE INDEX `IX_AccessPolicy_OrganizationUserId` ON `AccessPolicy` (`OrganizationUserId`);

CREATE INDEX `IX_AccessPolicy_ServiceAccountId` ON `AccessPolicy` (`ServiceAccountId`);

CREATE INDEX `IX_ApiKey_ServiceAccountId` ON `ApiKey` (`ServiceAccountId`);

CREATE INDEX `IX_Project_DeletedDate` ON `Project` (`DeletedDate`);

CREATE INDEX `IX_Project_OrganizationId` ON `Project` (`OrganizationId`);

CREATE INDEX `IX_ProjectSecret_SecretsId` ON `ProjectSecret` (`SecretsId`);

CREATE INDEX `IX_Secret_DeletedDate` ON `Secret` (`DeletedDate`);

CREATE INDEX `IX_Secret_OrganizationId` ON `Secret` (`OrganizationId`);

CREATE INDEX `IX_ServiceAccount_OrganizationId` ON `ServiceAccount` (`OrganizationId`);

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20230106122006_SecretsManager', '6.0.12');

COMMIT;
