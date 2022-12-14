START TRANSACTION;

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

CREATE INDEX `IX_AccessPolicy_GrantedProjectId` ON `AccessPolicy` (`GrantedProjectId`);

CREATE INDEX `IX_AccessPolicy_GrantedServiceAccountId` ON `AccessPolicy` (`GrantedServiceAccountId`);

CREATE INDEX `IX_AccessPolicy_GroupId` ON `AccessPolicy` (`GroupId`);

CREATE INDEX `IX_AccessPolicy_OrganizationUserId` ON `AccessPolicy` (`OrganizationUserId`);

CREATE INDEX `IX_AccessPolicy_ServiceAccountId` ON `AccessPolicy` (`ServiceAccountId`);

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20221125153348_AccessPolicy', '6.0.4');

COMMIT;
