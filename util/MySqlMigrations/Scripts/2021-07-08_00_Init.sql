ALTER DATABASE CHARACTER SET utf8mb4;
CREATE TABLE IF NOT EXISTS `__EFMigrationsHistory` (
    `MigrationId` varchar(150) CHARACTER SET utf8mb4 NOT NULL,
    `ProductVersion` varchar(32) CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK___EFMigrationsHistory` PRIMARY KEY (`MigrationId`)
) CHARACTER SET utf8mb4;

START TRANSACTION;

ALTER DATABASE CHARACTER SET utf8mb4;

CREATE TABLE `Event` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `Date` datetime(6) NOT NULL,
    `Type` int NOT NULL,
    `UserId` char(36) COLLATE ascii_general_ci NULL,
    `OrganizationId` char(36) COLLATE ascii_general_ci NULL,
    `CipherId` char(36) COLLATE ascii_general_ci NULL,
    `CollectionId` char(36) COLLATE ascii_general_ci NULL,
    `PolicyId` char(36) COLLATE ascii_general_ci NULL,
    `GroupId` char(36) COLLATE ascii_general_ci NULL,
    `OrganizationUserId` char(36) COLLATE ascii_general_ci NULL,
    `DeviceType` tinyint unsigned NULL,
    `IpAddress` varchar(50) CHARACTER SET utf8mb4 NULL,
    `ActingUserId` char(36) COLLATE ascii_general_ci NULL,
    CONSTRAINT `PK_Event` PRIMARY KEY (`Id`)
) ENGINE=InnoDB CHARACTER SET utf8mb4;

CREATE TABLE `Grant` (
    `Key` varchar(200) CHARACTER SET utf8mb4 NOT NULL,
    `Type` varchar(50) CHARACTER SET utf8mb4 NULL,
    `SubjectId` varchar(200) CHARACTER SET utf8mb4 NULL,
    `SessionId` varchar(100) CHARACTER SET utf8mb4 NULL,
    `ClientId` varchar(200) CHARACTER SET utf8mb4 NULL,
    `Description` varchar(200) CHARACTER SET utf8mb4 NULL,
    `CreationDate` datetime(6) NOT NULL,
    `ExpirationDate` datetime(6) NULL,
    `ConsumedDate` datetime(6) NULL,
    `Data` longtext CHARACTER SET utf8mb4 NULL,
    CONSTRAINT `PK_Grant` PRIMARY KEY (`Key`)
) ENGINE=InnoDB CHARACTER SET utf8mb4;

CREATE TABLE `Installation` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `Email` varchar(256) CHARACTER SET utf8mb4 NULL,
    `Key` varchar(150) CHARACTER SET utf8mb4 NULL,
    `Enabled` tinyint(1) NOT NULL,
    `CreationDate` datetime(6) NOT NULL,
    CONSTRAINT `PK_Installation` PRIMARY KEY (`Id`)
) ENGINE=InnoDB CHARACTER SET utf8mb4;

CREATE TABLE `Organization` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `Identifier` varchar(50) CHARACTER SET utf8mb4 NULL,
    `Name` varchar(50) CHARACTER SET utf8mb4 NULL,
    `BusinessName` varchar(50) CHARACTER SET utf8mb4 NULL,
    `BusinessAddress1` varchar(50) CHARACTER SET utf8mb4 NULL,
    `BusinessAddress2` varchar(50) CHARACTER SET utf8mb4 NULL,
    `BusinessAddress3` varchar(50) CHARACTER SET utf8mb4 NULL,
    `BusinessCountry` varchar(2) CHARACTER SET utf8mb4 NULL,
    `BusinessTaxNumber` varchar(30) CHARACTER SET utf8mb4 NULL,
    `BillingEmail` varchar(256) CHARACTER SET utf8mb4 NULL,
    `Plan` varchar(50) CHARACTER SET utf8mb4 NULL,
    `PlanType` tinyint unsigned NOT NULL,
    `Seats` int NULL,
    `MaxCollections` smallint NULL,
    `UsePolicies` tinyint(1) NOT NULL,
    `UseSso` tinyint(1) NOT NULL,
    `UseGroups` tinyint(1) NOT NULL,
    `UseDirectory` tinyint(1) NOT NULL,
    `UseEvents` tinyint(1) NOT NULL,
    `UseTotp` tinyint(1) NOT NULL,
    `Use2fa` tinyint(1) NOT NULL,
    `UseApi` tinyint(1) NOT NULL,
    `UseResetPassword` tinyint(1) NOT NULL,
    `SelfHost` tinyint(1) NOT NULL,
    `UsersGetPremium` tinyint(1) NOT NULL,
    `Storage` bigint NULL,
    `MaxStorageGb` smallint NULL,
    `Gateway` tinyint unsigned NULL,
    `GatewayCustomerId` varchar(50) CHARACTER SET utf8mb4 NULL,
    `GatewaySubscriptionId` varchar(50) CHARACTER SET utf8mb4 NULL,
    `ReferenceData` longtext CHARACTER SET utf8mb4 NULL,
    `Enabled` tinyint(1) NOT NULL,
    `LicenseKey` varchar(100) CHARACTER SET utf8mb4 NULL,
    `ApiKey` varchar(30) CHARACTER SET utf8mb4 NULL,
    `PublicKey` longtext CHARACTER SET utf8mb4 NULL,
    `PrivateKey` longtext CHARACTER SET utf8mb4 NULL,
    `TwoFactorProviders` longtext CHARACTER SET utf8mb4 NULL,
    `ExpirationDate` datetime(6) NULL,
    `CreationDate` datetime(6) NOT NULL,
    `RevisionDate` datetime(6) NOT NULL,
    CONSTRAINT `PK_Organization` PRIMARY KEY (`Id`)
) ENGINE=InnoDB CHARACTER SET utf8mb4;

CREATE TABLE `Provider` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `Name` longtext CHARACTER SET utf8mb4 NULL,
    `BusinessName` longtext CHARACTER SET utf8mb4 NULL,
    `BusinessAddress1` longtext CHARACTER SET utf8mb4 NULL,
    `BusinessAddress2` longtext CHARACTER SET utf8mb4 NULL,
    `BusinessAddress3` longtext CHARACTER SET utf8mb4 NULL,
    `BusinessCountry` longtext CHARACTER SET utf8mb4 NULL,
    `BusinessTaxNumber` longtext CHARACTER SET utf8mb4 NULL,
    `BillingEmail` longtext CHARACTER SET utf8mb4 NULL,
    `Status` tinyint unsigned NOT NULL,
    `Enabled` tinyint(1) NOT NULL,
    `CreationDate` datetime(6) NOT NULL,
    `RevisionDate` datetime(6) NOT NULL,
    CONSTRAINT `PK_Provider` PRIMARY KEY (`Id`)
) ENGINE=InnoDB CHARACTER SET utf8mb4;

CREATE TABLE `TaxRate` (
    `Id` varchar(40) CHARACTER SET utf8mb4 NOT NULL,
    `Country` varchar(50) CHARACTER SET utf8mb4 NULL,
    `State` varchar(2) CHARACTER SET utf8mb4 NULL,
    `PostalCode` varchar(10) CHARACTER SET utf8mb4 NULL,
    `Rate` decimal(65,30) NOT NULL,
    `Active` tinyint(1) NOT NULL,
    CONSTRAINT `PK_TaxRate` PRIMARY KEY (`Id`)
) ENGINE=InnoDB CHARACTER SET utf8mb4;

CREATE TABLE `User` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `Name` varchar(50) CHARACTER SET utf8mb4 NULL,
    `Email` varchar(256) CHARACTER SET utf8mb4 NOT NULL,
    `EmailVerified` tinyint(1) NOT NULL,
    `MasterPassword` varchar(300) CHARACTER SET utf8mb4 NULL,
    `MasterPasswordHint` varchar(50) CHARACTER SET utf8mb4 NULL,
    `Culture` varchar(10) CHARACTER SET utf8mb4 NULL,
    `SecurityStamp` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
    `TwoFactorProviders` longtext CHARACTER SET utf8mb4 NULL,
    `TwoFactorRecoveryCode` varchar(32) CHARACTER SET utf8mb4 NULL,
    `EquivalentDomains` longtext CHARACTER SET utf8mb4 NULL,
    `ExcludedGlobalEquivalentDomains` longtext CHARACTER SET utf8mb4 NULL,
    `AccountRevisionDate` datetime(6) NOT NULL,
    `Key` longtext CHARACTER SET utf8mb4 NULL,
    `PublicKey` longtext CHARACTER SET utf8mb4 NULL,
    `PrivateKey` longtext CHARACTER SET utf8mb4 NULL,
    `Premium` tinyint(1) NOT NULL,
    `PremiumExpirationDate` datetime(6) NULL,
    `RenewalReminderDate` datetime(6) NULL,
    `Storage` bigint NULL,
    `MaxStorageGb` smallint NULL,
    `Gateway` tinyint unsigned NULL,
    `GatewayCustomerId` varchar(50) CHARACTER SET utf8mb4 NULL,
    `GatewaySubscriptionId` varchar(50) CHARACTER SET utf8mb4 NULL,
    `ReferenceData` longtext CHARACTER SET utf8mb4 NULL,
    `LicenseKey` varchar(100) CHARACTER SET utf8mb4 NULL,
    `ApiKey` varchar(30) CHARACTER SET utf8mb4 NOT NULL,
    `Kdf` tinyint unsigned NOT NULL,
    `KdfIterations` int NOT NULL,
    `CreationDate` datetime(6) NOT NULL,
    `RevisionDate` datetime(6) NOT NULL,
    CONSTRAINT `PK_User` PRIMARY KEY (`Id`)
) ENGINE=InnoDB CHARACTER SET utf8mb4;

CREATE TABLE `Collection` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `OrganizationId` char(36) COLLATE ascii_general_ci NOT NULL,
    `Name` longtext CHARACTER SET utf8mb4 NULL,
    `ExternalId` varchar(300) CHARACTER SET utf8mb4 NULL,
    `CreationDate` datetime(6) NOT NULL,
    `RevisionDate` datetime(6) NOT NULL,
    CONSTRAINT `PK_Collection` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_Collection_Organization_OrganizationId` FOREIGN KEY (`OrganizationId`) REFERENCES `Organization` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB CHARACTER SET utf8mb4;

CREATE TABLE `Group` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `OrganizationId` char(36) COLLATE ascii_general_ci NOT NULL,
    `Name` varchar(100) CHARACTER SET utf8mb4 NULL,
    `AccessAll` tinyint(1) NOT NULL,
    `ExternalId` varchar(300) CHARACTER SET utf8mb4 NULL,
    `CreationDate` datetime(6) NOT NULL,
    `RevisionDate` datetime(6) NOT NULL,
    CONSTRAINT `PK_Group` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_Group_Organization_OrganizationId` FOREIGN KEY (`OrganizationId`) REFERENCES `Organization` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB CHARACTER SET utf8mb4;

CREATE TABLE `Policy` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `OrganizationId` char(36) COLLATE ascii_general_ci NOT NULL,
    `Type` tinyint unsigned NOT NULL,
    `Data` longtext CHARACTER SET utf8mb4 NULL,
    `Enabled` tinyint(1) NOT NULL,
    `CreationDate` datetime(6) NOT NULL,
    `RevisionDate` datetime(6) NOT NULL,
    CONSTRAINT `PK_Policy` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_Policy_Organization_OrganizationId` FOREIGN KEY (`OrganizationId`) REFERENCES `Organization` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB CHARACTER SET utf8mb4;

CREATE TABLE `SsoConfig` (
    `Id` bigint NOT NULL AUTO_INCREMENT,
    `Enabled` tinyint(1) NOT NULL,
    `OrganizationId` char(36) COLLATE ascii_general_ci NOT NULL,
    `Data` longtext CHARACTER SET utf8mb4 NULL,
    `CreationDate` datetime(6) NOT NULL,
    `RevisionDate` datetime(6) NOT NULL,
    CONSTRAINT `PK_SsoConfig` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_SsoConfig_Organization_OrganizationId` FOREIGN KEY (`OrganizationId`) REFERENCES `Organization` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB CHARACTER SET utf8mb4;

CREATE TABLE `ProviderOrganization` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `ProviderId` char(36) COLLATE ascii_general_ci NOT NULL,
    `OrganizationId` char(36) COLLATE ascii_general_ci NOT NULL,
    `Key` longtext CHARACTER SET utf8mb4 NULL,
    `Settings` longtext CHARACTER SET utf8mb4 NULL,
    `CreationDate` datetime(6) NOT NULL,
    `RevisionDate` datetime(6) NOT NULL,
    CONSTRAINT `PK_ProviderOrganization` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_ProviderOrganization_Organization_OrganizationId` FOREIGN KEY (`OrganizationId`) REFERENCES `Organization` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_ProviderOrganization_Provider_ProviderId` FOREIGN KEY (`ProviderId`) REFERENCES `Provider` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB CHARACTER SET utf8mb4;

CREATE TABLE `Cipher` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `UserId` char(36) COLLATE ascii_general_ci NULL,
    `OrganizationId` char(36) COLLATE ascii_general_ci NULL,
    `Type` tinyint unsigned NOT NULL,
    `Data` longtext CHARACTER SET utf8mb4 NULL,
    `Favorites` longtext CHARACTER SET utf8mb4 NULL,
    `Folders` longtext CHARACTER SET utf8mb4 NULL,
    `Attachments` longtext CHARACTER SET utf8mb4 NULL,
    `CreationDate` datetime(6) NOT NULL,
    `RevisionDate` datetime(6) NOT NULL,
    `DeletedDate` datetime(6) NULL,
    `Reprompt` tinyint unsigned NULL,
    CONSTRAINT `PK_Cipher` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_Cipher_Organization_OrganizationId` FOREIGN KEY (`OrganizationId`) REFERENCES `Organization` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_Cipher_User_UserId` FOREIGN KEY (`UserId`) REFERENCES `User` (`Id`) ON DELETE RESTRICT
) ENGINE=InnoDB CHARACTER SET utf8mb4;

CREATE TABLE `Device` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `UserId` char(36) COLLATE ascii_general_ci NOT NULL,
    `Name` varchar(50) CHARACTER SET utf8mb4 NULL,
    `Type` tinyint unsigned NOT NULL,
    `Identifier` varchar(50) CHARACTER SET utf8mb4 NULL,
    `PushToken` varchar(255) CHARACTER SET utf8mb4 NULL,
    `CreationDate` datetime(6) NOT NULL,
    `RevisionDate` datetime(6) NOT NULL,
    CONSTRAINT `PK_Device` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_Device_User_UserId` FOREIGN KEY (`UserId`) REFERENCES `User` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB CHARACTER SET utf8mb4;

CREATE TABLE `EmergencyAccess` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `GrantorId` char(36) COLLATE ascii_general_ci NOT NULL,
    `GranteeId` char(36) COLLATE ascii_general_ci NULL,
    `Email` varchar(256) CHARACTER SET utf8mb4 NULL,
    `KeyEncrypted` longtext CHARACTER SET utf8mb4 NULL,
    `Type` tinyint unsigned NOT NULL,
    `Status` tinyint unsigned NOT NULL,
    `WaitTimeDays` int NOT NULL,
    `RecoveryInitiatedDate` datetime(6) NULL,
    `LastNotificationDate` datetime(6) NULL,
    `CreationDate` datetime(6) NOT NULL,
    `RevisionDate` datetime(6) NOT NULL,
    CONSTRAINT `PK_EmergencyAccess` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_EmergencyAccess_User_GranteeId` FOREIGN KEY (`GranteeId`) REFERENCES `User` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_EmergencyAccess_User_GrantorId` FOREIGN KEY (`GrantorId`) REFERENCES `User` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB CHARACTER SET utf8mb4;

CREATE TABLE `Folder` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `UserId` char(36) COLLATE ascii_general_ci NOT NULL,
    `Name` longtext CHARACTER SET utf8mb4 NULL,
    `CreationDate` datetime(6) NOT NULL,
    `RevisionDate` datetime(6) NOT NULL,
    CONSTRAINT `PK_Folder` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_Folder_User_UserId` FOREIGN KEY (`UserId`) REFERENCES `User` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB CHARACTER SET utf8mb4;

CREATE TABLE `OrganizationUser` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `OrganizationId` char(36) COLLATE ascii_general_ci NOT NULL,
    `UserId` char(36) COLLATE ascii_general_ci NULL,
    `Email` varchar(256) CHARACTER SET utf8mb4 NULL,
    `Key` longtext CHARACTER SET utf8mb4 NULL,
    `ResetPasswordKey` longtext CHARACTER SET utf8mb4 NULL,
    `Status` tinyint unsigned NOT NULL,
    `Type` tinyint unsigned NOT NULL,
    `AccessAll` tinyint(1) NOT NULL,
    `ExternalId` varchar(300) CHARACTER SET utf8mb4 NULL,
    `CreationDate` datetime(6) NOT NULL,
    `RevisionDate` datetime(6) NOT NULL,
    `Permissions` longtext CHARACTER SET utf8mb4 NULL,
    CONSTRAINT `PK_OrganizationUser` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_OrganizationUser_Organization_OrganizationId` FOREIGN KEY (`OrganizationId`) REFERENCES `Organization` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_OrganizationUser_User_UserId` FOREIGN KEY (`UserId`) REFERENCES `User` (`Id`) ON DELETE RESTRICT
) ENGINE=InnoDB CHARACTER SET utf8mb4;

CREATE TABLE `ProviderUser` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `ProviderId` char(36) COLLATE ascii_general_ci NOT NULL,
    `UserId` char(36) COLLATE ascii_general_ci NULL,
    `Email` longtext CHARACTER SET utf8mb4 NULL,
    `Key` longtext CHARACTER SET utf8mb4 NULL,
    `Status` tinyint unsigned NOT NULL,
    `Type` tinyint unsigned NOT NULL,
    `Permissions` longtext CHARACTER SET utf8mb4 NULL,
    `CreationDate` datetime(6) NOT NULL,
    `RevisionDate` datetime(6) NOT NULL,
    CONSTRAINT `PK_ProviderUser` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_ProviderUser_Provider_ProviderId` FOREIGN KEY (`ProviderId`) REFERENCES `Provider` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_ProviderUser_User_UserId` FOREIGN KEY (`UserId`) REFERENCES `User` (`Id`) ON DELETE RESTRICT
) ENGINE=InnoDB CHARACTER SET utf8mb4;

CREATE TABLE `Send` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `UserId` char(36) COLLATE ascii_general_ci NULL,
    `OrganizationId` char(36) COLLATE ascii_general_ci NULL,
    `Type` tinyint unsigned NOT NULL,
    `Data` longtext CHARACTER SET utf8mb4 NULL,
    `Key` longtext CHARACTER SET utf8mb4 NULL,
    `Password` varchar(300) CHARACTER SET utf8mb4 NULL,
    `MaxAccessCount` int NULL,
    `AccessCount` int NOT NULL,
    `CreationDate` datetime(6) NOT NULL,
    `RevisionDate` datetime(6) NOT NULL,
    `ExpirationDate` datetime(6) NULL,
    `DeletionDate` datetime(6) NOT NULL,
    `Disabled` tinyint(1) NOT NULL,
    `HideEmail` tinyint(1) NULL,
    CONSTRAINT `PK_Send` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_Send_Organization_OrganizationId` FOREIGN KEY (`OrganizationId`) REFERENCES `Organization` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_Send_User_UserId` FOREIGN KEY (`UserId`) REFERENCES `User` (`Id`) ON DELETE RESTRICT
) ENGINE=InnoDB CHARACTER SET utf8mb4;

CREATE TABLE `SsoUser` (
    `Id` bigint NOT NULL AUTO_INCREMENT,
    `UserId` char(36) COLLATE ascii_general_ci NOT NULL,
    `OrganizationId` char(36) COLLATE ascii_general_ci NULL,
    `ExternalId` varchar(50) CHARACTER SET utf8mb4 NULL,
    `CreationDate` datetime(6) NOT NULL,
    CONSTRAINT `PK_SsoUser` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_SsoUser_Organization_OrganizationId` FOREIGN KEY (`OrganizationId`) REFERENCES `Organization` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_SsoUser_User_UserId` FOREIGN KEY (`UserId`) REFERENCES `User` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB CHARACTER SET utf8mb4;

CREATE TABLE `Transaction` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `UserId` char(36) COLLATE ascii_general_ci NULL,
    `OrganizationId` char(36) COLLATE ascii_general_ci NULL,
    `Type` tinyint unsigned NOT NULL,
    `Amount` decimal(65,30) NOT NULL,
    `Refunded` tinyint(1) NULL,
    `RefundedAmount` decimal(65,30) NULL,
    `Details` varchar(100) CHARACTER SET utf8mb4 NULL,
    `PaymentMethodType` tinyint unsigned NULL,
    `Gateway` tinyint unsigned NULL,
    `GatewayId` varchar(50) CHARACTER SET utf8mb4 NULL,
    `CreationDate` datetime(6) NOT NULL,
    CONSTRAINT `PK_Transaction` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_Transaction_Organization_OrganizationId` FOREIGN KEY (`OrganizationId`) REFERENCES `Organization` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_Transaction_User_UserId` FOREIGN KEY (`UserId`) REFERENCES `User` (`Id`) ON DELETE RESTRICT
) ENGINE=InnoDB CHARACTER SET utf8mb4;

CREATE TABLE `U2f` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `UserId` char(36) COLLATE ascii_general_ci NOT NULL,
    `KeyHandle` varchar(200) CHARACTER SET utf8mb4 NULL,
    `Challenge` varchar(200) CHARACTER SET utf8mb4 NULL,
    `AppId` varchar(50) CHARACTER SET utf8mb4 NULL,
    `Version` varchar(20) CHARACTER SET utf8mb4 NULL,
    `CreationDate` datetime(6) NOT NULL,
    CONSTRAINT `PK_U2f` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_U2f_User_UserId` FOREIGN KEY (`UserId`) REFERENCES `User` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB CHARACTER SET utf8mb4;

CREATE TABLE `CollectionGroups` (
    `CollectionId` char(36) COLLATE ascii_general_ci NOT NULL,
    `GroupId` char(36) COLLATE ascii_general_ci NOT NULL,
    `ReadOnly` tinyint(1) NOT NULL,
    `HidePasswords` tinyint(1) NOT NULL,
    CONSTRAINT `PK_CollectionGroups` PRIMARY KEY (`CollectionId`, `GroupId`),
    CONSTRAINT `FK_CollectionGroups_Collection_CollectionId` FOREIGN KEY (`CollectionId`) REFERENCES `Collection` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_CollectionGroups_Group_GroupId` FOREIGN KEY (`GroupId`) REFERENCES `Group` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB CHARACTER SET utf8mb4;

CREATE TABLE `CollectionCipher` (
    `CollectionId` char(36) COLLATE ascii_general_ci NOT NULL,
    `CipherId` char(36) COLLATE ascii_general_ci NOT NULL,
    CONSTRAINT `PK_CollectionCipher` PRIMARY KEY (`CollectionId`, `CipherId`),
    CONSTRAINT `FK_CollectionCipher_Cipher_CipherId` FOREIGN KEY (`CipherId`) REFERENCES `Cipher` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_CollectionCipher_Collection_CollectionId` FOREIGN KEY (`CollectionId`) REFERENCES `Collection` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB CHARACTER SET utf8mb4;

CREATE TABLE `CollectionUsers` (
    `CollectionId` char(36) COLLATE ascii_general_ci NOT NULL,
    `OrganizationUserId` char(36) COLLATE ascii_general_ci NOT NULL,
    `UserId` char(36) COLLATE ascii_general_ci NULL,
    `ReadOnly` tinyint(1) NOT NULL,
    `HidePasswords` tinyint(1) NOT NULL,
    CONSTRAINT `PK_CollectionUsers` PRIMARY KEY (`CollectionId`, `OrganizationUserId`),
    CONSTRAINT `FK_CollectionUsers_Collection_CollectionId` FOREIGN KEY (`CollectionId`) REFERENCES `Collection` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_CollectionUsers_OrganizationUser_OrganizationUserId` FOREIGN KEY (`OrganizationUserId`) REFERENCES `OrganizationUser` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_CollectionUsers_User_UserId` FOREIGN KEY (`UserId`) REFERENCES `User` (`Id`) ON DELETE RESTRICT
) ENGINE=InnoDB CHARACTER SET utf8mb4;

CREATE TABLE `GroupUser` (
    `GroupId` char(36) COLLATE ascii_general_ci NOT NULL,
    `OrganizationUserId` char(36) COLLATE ascii_general_ci NOT NULL,
    `UserId` char(36) COLLATE ascii_general_ci NULL,
    CONSTRAINT `PK_GroupUser` PRIMARY KEY (`GroupId`, `OrganizationUserId`),
    CONSTRAINT `FK_GroupUser_Group_GroupId` FOREIGN KEY (`GroupId`) REFERENCES `Group` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_GroupUser_OrganizationUser_OrganizationUserId` FOREIGN KEY (`OrganizationUserId`) REFERENCES `OrganizationUser` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_GroupUser_User_UserId` FOREIGN KEY (`UserId`) REFERENCES `User` (`Id`) ON DELETE RESTRICT
) ENGINE=InnoDB CHARACTER SET utf8mb4;

CREATE TABLE `ProviderOrganizationProviderUser` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `ProviderOrganizationId` char(36) COLLATE ascii_general_ci NOT NULL,
    `ProviderUserId` char(36) COLLATE ascii_general_ci NOT NULL,
    `Type` tinyint unsigned NOT NULL,
    `Permissions` longtext CHARACTER SET utf8mb4 NULL,
    `CreationDate` datetime(6) NOT NULL,
    `RevisionDate` datetime(6) NOT NULL,
    CONSTRAINT `PK_ProviderOrganizationProviderUser` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_ProviderOrganizationProviderUser_ProviderOrganization_Provid~` FOREIGN KEY (`ProviderOrganizationId`) REFERENCES `ProviderOrganization` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_ProviderOrganizationProviderUser_ProviderUser_ProviderUserId` FOREIGN KEY (`ProviderUserId`) REFERENCES `ProviderUser` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB CHARACTER SET utf8mb4;

CREATE INDEX `IX_Cipher_OrganizationId` ON `Cipher` (`OrganizationId`);

CREATE INDEX `IX_Cipher_UserId` ON `Cipher` (`UserId`);

CREATE INDEX `IX_Collection_OrganizationId` ON `Collection` (`OrganizationId`);

CREATE INDEX `IX_CollectionCipher_CipherId` ON `CollectionCipher` (`CipherId`);

CREATE INDEX `IX_CollectionGroups_GroupId` ON `CollectionGroups` (`GroupId`);

CREATE INDEX `IX_CollectionUsers_OrganizationUserId` ON `CollectionUsers` (`OrganizationUserId`);

CREATE INDEX `IX_CollectionUsers_UserId` ON `CollectionUsers` (`UserId`);

CREATE INDEX `IX_Device_UserId` ON `Device` (`UserId`);

CREATE INDEX `IX_EmergencyAccess_GranteeId` ON `EmergencyAccess` (`GranteeId`);

CREATE INDEX `IX_EmergencyAccess_GrantorId` ON `EmergencyAccess` (`GrantorId`);

CREATE INDEX `IX_Folder_UserId` ON `Folder` (`UserId`);

CREATE INDEX `IX_Group_OrganizationId` ON `Group` (`OrganizationId`);

CREATE INDEX `IX_GroupUser_OrganizationUserId` ON `GroupUser` (`OrganizationUserId`);

CREATE INDEX `IX_GroupUser_UserId` ON `GroupUser` (`UserId`);

CREATE INDEX `IX_OrganizationUser_OrganizationId` ON `OrganizationUser` (`OrganizationId`);

CREATE INDEX `IX_OrganizationUser_UserId` ON `OrganizationUser` (`UserId`);

CREATE INDEX `IX_Policy_OrganizationId` ON `Policy` (`OrganizationId`);

CREATE INDEX `IX_ProviderOrganization_OrganizationId` ON `ProviderOrganization` (`OrganizationId`);

CREATE INDEX `IX_ProviderOrganization_ProviderId` ON `ProviderOrganization` (`ProviderId`);

CREATE INDEX `IX_ProviderOrganizationProviderUser_ProviderOrganizationId` ON `ProviderOrganizationProviderUser` (`ProviderOrganizationId`);

CREATE INDEX `IX_ProviderOrganizationProviderUser_ProviderUserId` ON `ProviderOrganizationProviderUser` (`ProviderUserId`);

CREATE INDEX `IX_ProviderUser_ProviderId` ON `ProviderUser` (`ProviderId`);

CREATE INDEX `IX_ProviderUser_UserId` ON `ProviderUser` (`UserId`);

CREATE INDEX `IX_Send_OrganizationId` ON `Send` (`OrganizationId`);

CREATE INDEX `IX_Send_UserId` ON `Send` (`UserId`);

CREATE INDEX `IX_SsoConfig_OrganizationId` ON `SsoConfig` (`OrganizationId`);

CREATE INDEX `IX_SsoUser_OrganizationId` ON `SsoUser` (`OrganizationId`);

CREATE INDEX `IX_SsoUser_UserId` ON `SsoUser` (`UserId`);

CREATE INDEX `IX_Transaction_OrganizationId` ON `Transaction` (`OrganizationId`);

CREATE INDEX `IX_Transaction_UserId` ON `Transaction` (`UserId`);

CREATE INDEX `IX_U2f_UserId` ON `U2f` (`UserId`);

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20210617183900_Init', '5.0.5');

COMMIT;
