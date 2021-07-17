ALTER DATABASE CHARACTER SET utf8mb4;
CREATE TABLE IF NOT EXISTS `__EFMigrationsHistory` (
    `MigrationId` varchar(150) CHARACTER SET utf8mb4 NOT NULL,
    `ProductVersion` varchar(32) CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK___EFMigrationsHistory` PRIMARY KEY (`MigrationId`)
) CHARACTER SET utf8mb4;

START TRANSACTION;

CREATE TABLE `Event` (
    `Id` uuid NOT NULL,
    `Date` timestamp without time zone NOT NULL,
    `Type` integer NOT NULL,
    `UserId` uuid NULL,
    `OrganizationId` uuid NULL,
    `CipherId` uuid NULL,
    `CollectionId` uuid NULL,
    `PolicyId` uuid NULL,
    `GroupId` uuid NULL,
    `OrganizationUserId` uuid NULL,
    `DeviceType` smallint NULL,
    `IpAddress` character varying(50) NULL,
    `ActingUserId` uuid NULL,
    CONSTRAINT `PK_Event` PRIMARY KEY (`Id`)
) ENGINE=InnoDB;

CREATE TABLE `Grant` (
    `Key` character varying(200) NOT NULL,
    `Type` character varying(50) NULL,
    `SubjectId` character varying(200) NULL,
    `SessionId` character varying(100) NULL,
    `ClientId` character varying(200) NULL,
    `Description` character varying(200) NULL,
    `CreationDate` timestamp without time zone NOT NULL,
    `ExpirationDate` timestamp without time zone NULL,
    `ConsumedDate` timestamp without time zone NULL,
    `Data` text NULL,
    CONSTRAINT `PK_Grant` PRIMARY KEY (`Key`)
) ENGINE=InnoDB;

CREATE TABLE `Installation` (
    `Id` uuid NOT NULL,
    `Email` character varying(256) NULL,
    `Key` character varying(150) NULL,
    `Enabled` boolean NOT NULL,
    `CreationDate` timestamp without time zone NOT NULL,
    CONSTRAINT `PK_Installation` PRIMARY KEY (`Id`)
) ENGINE=InnoDB;

CREATE TABLE `Organization` (
    `Id` uuid NOT NULL,
    `Identifier` character varying(50) COLLATE postgresIndetermanisticCollation NULL,
    `Name` character varying(50) NULL,
    `BusinessName` character varying(50) NULL,
    `BusinessAddress1` character varying(50) NULL,
    `BusinessAddress2` character varying(50) NULL,
    `BusinessAddress3` character varying(50) NULL,
    `BusinessCountry` character varying(2) NULL,
    `BusinessTaxNumber` character varying(30) NULL,
    `BillingEmail` character varying(256) NULL,
    `Plan` character varying(50) NULL,
    `PlanType` smallint NOT NULL,
    `Seats` integer NULL,
    `MaxCollections` smallint NULL,
    `UsePolicies` boolean NOT NULL,
    `UseSso` boolean NOT NULL,
    `UseGroups` boolean NOT NULL,
    `UseDirectory` boolean NOT NULL,
    `UseEvents` boolean NOT NULL,
    `UseTotp` boolean NOT NULL,
    `Use2fa` boolean NOT NULL,
    `UseApi` boolean NOT NULL,
    `UseResetPassword` boolean NOT NULL,
    `SelfHost` boolean NOT NULL,
    `UsersGetPremium` boolean NOT NULL,
    `Storage` bigint NULL,
    `MaxStorageGb` smallint NULL,
    `Gateway` smallint NULL,
    `GatewayCustomerId` character varying(50) NULL,
    `GatewaySubscriptionId` character varying(50) NULL,
    `ReferenceData` text NULL,
    `Enabled` boolean NOT NULL,
    `LicenseKey` character varying(100) NULL,
    `ApiKey` character varying(30) NULL,
    `PublicKey` text NULL,
    `PrivateKey` text NULL,
    `TwoFactorProviders` text NULL,
    `ExpirationDate` timestamp without time zone NULL,
    `CreationDate` timestamp without time zone NOT NULL,
    `RevisionDate` timestamp without time zone NOT NULL,
    CONSTRAINT `PK_Organization` PRIMARY KEY (`Id`)
) ENGINE=InnoDB;

CREATE TABLE `Provider` (
    `Id` uuid NOT NULL,
    `Name` text NULL,
    `BusinessName` text NULL,
    `BusinessAddress1` text NULL,
    `BusinessAddress2` text NULL,
    `BusinessAddress3` text NULL,
    `BusinessCountry` text NULL,
    `BusinessTaxNumber` text NULL,
    `BillingEmail` text NULL,
    `Status` smallint NOT NULL,
    `Enabled` boolean NOT NULL,
    `CreationDate` timestamp without time zone NOT NULL,
    `RevisionDate` timestamp without time zone NOT NULL,
    CONSTRAINT `PK_Provider` PRIMARY KEY (`Id`)
) ENGINE=InnoDB;

CREATE TABLE `TaxRate` (
    `Id` character varying(40) NOT NULL,
    `Country` character varying(50) NULL,
    `State` character varying(2) NULL,
    `PostalCode` character varying(10) NULL,
    `Rate` numeric NOT NULL,
    `Active` boolean NOT NULL,
    CONSTRAINT `PK_TaxRate` PRIMARY KEY (`Id`)
) ENGINE=InnoDB;

CREATE TABLE `User` (
    `Id` uuid NOT NULL,
    `Name` character varying(50) NULL,
    `Email` character varying(256) COLLATE postgresIndetermanisticCollation NOT NULL,
    `EmailVerified` boolean NOT NULL,
    `MasterPassword` character varying(300) NULL,
    `MasterPasswordHint` character varying(50) NULL,
    `Culture` character varying(10) NULL,
    `SecurityStamp` character varying(50) NOT NULL,
    `TwoFactorProviders` text NULL,
    `TwoFactorRecoveryCode` character varying(32) NULL,
    `EquivalentDomains` text NULL,
    `ExcludedGlobalEquivalentDomains` text NULL,
    `AccountRevisionDate` timestamp without time zone NOT NULL,
    `Key` text NULL,
    `PublicKey` text NULL,
    `PrivateKey` text NULL,
    `Premium` boolean NOT NULL,
    `PremiumExpirationDate` timestamp without time zone NULL,
    `RenewalReminderDate` timestamp without time zone NULL,
    `Storage` bigint NULL,
    `MaxStorageGb` smallint NULL,
    `Gateway` smallint NULL,
    `GatewayCustomerId` character varying(50) NULL,
    `GatewaySubscriptionId` character varying(50) NULL,
    `ReferenceData` text NULL,
    `LicenseKey` character varying(100) NULL,
    `ApiKey` character varying(30) NOT NULL,
    `Kdf` smallint NOT NULL,
    `KdfIterations` integer NOT NULL,
    `CreationDate` timestamp without time zone NOT NULL,
    `RevisionDate` timestamp without time zone NOT NULL,
    CONSTRAINT `PK_User` PRIMARY KEY (`Id`)
) ENGINE=InnoDB;

CREATE TABLE `Collection` (
    `Id` uuid NOT NULL,
    `OrganizationId` uuid NOT NULL,
    `Name` text NULL,
    `ExternalId` character varying(300) NULL,
    `CreationDate` timestamp without time zone NOT NULL,
    `RevisionDate` timestamp without time zone NOT NULL,
    CONSTRAINT `PK_Collection` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_Collection_Organization_OrganizationId` FOREIGN KEY (`OrganizationId`) REFERENCES `Organization` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB;

CREATE TABLE `Group` (
    `Id` uuid NOT NULL,
    `OrganizationId` uuid NOT NULL,
    `Name` character varying(100) NULL,
    `AccessAll` boolean NOT NULL,
    `ExternalId` character varying(300) NULL,
    `CreationDate` timestamp without time zone NOT NULL,
    `RevisionDate` timestamp without time zone NOT NULL,
    CONSTRAINT `PK_Group` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_Group_Organization_OrganizationId` FOREIGN KEY (`OrganizationId`) REFERENCES `Organization` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB;

CREATE TABLE `Policy` (
    `Id` uuid NOT NULL,
    `OrganizationId` uuid NOT NULL,
    `Type` smallint NOT NULL,
    `Data` text NULL,
    `Enabled` boolean NOT NULL,
    `CreationDate` timestamp without time zone NOT NULL,
    `RevisionDate` timestamp without time zone NOT NULL,
    CONSTRAINT `PK_Policy` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_Policy_Organization_OrganizationId` FOREIGN KEY (`OrganizationId`) REFERENCES `Organization` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB;

CREATE TABLE `SsoConfig` (
    `Id` bigint NOT NULL,
    `Enabled` boolean NOT NULL,
    `OrganizationId` uuid NOT NULL,
    `Data` text NULL,
    `CreationDate` timestamp without time zone NOT NULL,
    `RevisionDate` timestamp without time zone NOT NULL,
    CONSTRAINT `PK_SsoConfig` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_SsoConfig_Organization_OrganizationId` FOREIGN KEY (`OrganizationId`) REFERENCES `Organization` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB;

CREATE TABLE `ProviderOrganization` (
    `Id` uuid NOT NULL,
    `ProviderId` uuid NOT NULL,
    `OrganizationId` uuid NOT NULL,
    `Key` text NULL,
    `Settings` text NULL,
    `CreationDate` timestamp without time zone NOT NULL,
    `RevisionDate` timestamp without time zone NOT NULL,
    CONSTRAINT `PK_ProviderOrganization` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_ProviderOrganization_Organization_OrganizationId` FOREIGN KEY (`OrganizationId`) REFERENCES `Organization` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_ProviderOrganization_Provider_ProviderId` FOREIGN KEY (`ProviderId`) REFERENCES `Provider` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB;

CREATE TABLE `Cipher` (
    `Id` uuid NOT NULL,
    `UserId` uuid NULL,
    `OrganizationId` uuid NULL,
    `Type` smallint NOT NULL,
    `Data` text NULL,
    `Favorites` text NULL,
    `Folders` text NULL,
    `Attachments` text NULL,
    `CreationDate` timestamp without time zone NOT NULL,
    `RevisionDate` timestamp without time zone NOT NULL,
    `DeletedDate` timestamp without time zone NULL,
    `Reprompt` smallint NULL,
    CONSTRAINT `PK_Cipher` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_Cipher_Organization_OrganizationId` FOREIGN KEY (`OrganizationId`) REFERENCES `Organization` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_Cipher_User_UserId` FOREIGN KEY (`UserId`) REFERENCES `User` (`Id`) ON DELETE RESTRICT
) ENGINE=InnoDB;

CREATE TABLE `Device` (
    `Id` uuid NOT NULL,
    `UserId` uuid NOT NULL,
    `Name` character varying(50) NULL,
    `Type` smallint NOT NULL,
    `Identifier` character varying(50) NULL,
    `PushToken` character varying(255) NULL,
    `CreationDate` timestamp without time zone NOT NULL,
    `RevisionDate` timestamp without time zone NOT NULL,
    CONSTRAINT `PK_Device` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_Device_User_UserId` FOREIGN KEY (`UserId`) REFERENCES `User` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB;

CREATE TABLE `EmergencyAccess` (
    `Id` uuid NOT NULL,
    `GrantorId` uuid NOT NULL,
    `GranteeId` uuid NULL,
    `Email` character varying(256) NULL,
    `KeyEncrypted` text NULL,
    `Type` smallint NOT NULL,
    `Status` smallint NOT NULL,
    `WaitTimeDays` integer NOT NULL,
    `RecoveryInitiatedDate` timestamp without time zone NULL,
    `LastNotificationDate` timestamp without time zone NULL,
    `CreationDate` timestamp without time zone NOT NULL,
    `RevisionDate` timestamp without time zone NOT NULL,
    CONSTRAINT `PK_EmergencyAccess` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_EmergencyAccess_User_GranteeId` FOREIGN KEY (`GranteeId`) REFERENCES `User` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_EmergencyAccess_User_GrantorId` FOREIGN KEY (`GrantorId`) REFERENCES `User` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB;

CREATE TABLE `Folder` (
    `Id` uuid NOT NULL,
    `UserId` uuid NOT NULL,
    `Name` text NULL,
    `CreationDate` timestamp without time zone NOT NULL,
    `RevisionDate` timestamp without time zone NOT NULL,
    CONSTRAINT `PK_Folder` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_Folder_User_UserId` FOREIGN KEY (`UserId`) REFERENCES `User` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB;

CREATE TABLE `OrganizationUser` (
    `Id` uuid NOT NULL,
    `OrganizationId` uuid NOT NULL,
    `UserId` uuid NULL,
    `Email` character varying(256) NULL,
    `Key` text NULL,
    `ResetPasswordKey` text NULL,
    `Status` smallint NOT NULL,
    `Type` smallint NOT NULL,
    `AccessAll` boolean NOT NULL,
    `ExternalId` character varying(300) NULL,
    `CreationDate` timestamp without time zone NOT NULL,
    `RevisionDate` timestamp without time zone NOT NULL,
    `Permissions` text NULL,
    CONSTRAINT `PK_OrganizationUser` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_OrganizationUser_Organization_OrganizationId` FOREIGN KEY (`OrganizationId`) REFERENCES `Organization` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_OrganizationUser_User_UserId` FOREIGN KEY (`UserId`) REFERENCES `User` (`Id`) ON DELETE RESTRICT
) ENGINE=InnoDB;

CREATE TABLE `ProviderUser` (
    `Id` uuid NOT NULL,
    `ProviderId` uuid NOT NULL,
    `UserId` uuid NULL,
    `Email` text NULL,
    `Key` text NULL,
    `Status` smallint NOT NULL,
    `Type` smallint NOT NULL,
    `Permissions` text NULL,
    `CreationDate` timestamp without time zone NOT NULL,
    `RevisionDate` timestamp without time zone NOT NULL,
    CONSTRAINT `PK_ProviderUser` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_ProviderUser_Provider_ProviderId` FOREIGN KEY (`ProviderId`) REFERENCES `Provider` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_ProviderUser_User_UserId` FOREIGN KEY (`UserId`) REFERENCES `User` (`Id`) ON DELETE RESTRICT
) ENGINE=InnoDB;

CREATE TABLE `Send` (
    `Id` uuid NOT NULL,
    `UserId` uuid NULL,
    `OrganizationId` uuid NULL,
    `Type` smallint NOT NULL,
    `Data` text NULL,
    `Key` text NULL,
    `Password` character varying(300) NULL,
    `MaxAccessCount` integer NULL,
    `AccessCount` integer NOT NULL,
    `CreationDate` timestamp without time zone NOT NULL,
    `RevisionDate` timestamp without time zone NOT NULL,
    `ExpirationDate` timestamp without time zone NULL,
    `DeletionDate` timestamp without time zone NOT NULL,
    `Disabled` boolean NOT NULL,
    `HideEmail` boolean NULL,
    CONSTRAINT `PK_Send` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_Send_Organization_OrganizationId` FOREIGN KEY (`OrganizationId`) REFERENCES `Organization` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_Send_User_UserId` FOREIGN KEY (`UserId`) REFERENCES `User` (`Id`) ON DELETE RESTRICT
) ENGINE=InnoDB;

CREATE TABLE `SsoUser` (
    `Id` bigint NOT NULL,
    `UserId` uuid NOT NULL,
    `OrganizationId` uuid NULL,
    `ExternalId` character varying(50) COLLATE postgresIndetermanisticCollation NULL,
    `CreationDate` timestamp without time zone NOT NULL,
    CONSTRAINT `PK_SsoUser` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_SsoUser_Organization_OrganizationId` FOREIGN KEY (`OrganizationId`) REFERENCES `Organization` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_SsoUser_User_UserId` FOREIGN KEY (`UserId`) REFERENCES `User` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB;

CREATE TABLE `Transaction` (
    `Id` uuid NOT NULL,
    `UserId` uuid NULL,
    `OrganizationId` uuid NULL,
    `Type` smallint NOT NULL,
    `Amount` numeric NOT NULL,
    `Refunded` boolean NULL,
    `RefundedAmount` numeric NULL,
    `Details` character varying(100) NULL,
    `PaymentMethodType` smallint NULL,
    `Gateway` smallint NULL,
    `GatewayId` character varying(50) NULL,
    `CreationDate` timestamp without time zone NOT NULL,
    CONSTRAINT `PK_Transaction` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_Transaction_Organization_OrganizationId` FOREIGN KEY (`OrganizationId`) REFERENCES `Organization` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_Transaction_User_UserId` FOREIGN KEY (`UserId`) REFERENCES `User` (`Id`) ON DELETE RESTRICT
) ENGINE=InnoDB;

CREATE TABLE `U2f` (
    `Id` integer NOT NULL,
    `UserId` uuid NOT NULL,
    `KeyHandle` character varying(200) NULL,
    `Challenge` character varying(200) NULL,
    `AppId` character varying(50) NULL,
    `Version` character varying(20) NULL,
    `CreationDate` timestamp without time zone NOT NULL,
    CONSTRAINT `PK_U2f` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_U2f_User_UserId` FOREIGN KEY (`UserId`) REFERENCES `User` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB;

CREATE TABLE `CollectionGroups` (
    `CollectionId` uuid NOT NULL,
    `GroupId` uuid NOT NULL,
    `ReadOnly` boolean NOT NULL,
    `HidePasswords` boolean NOT NULL,
    CONSTRAINT `PK_CollectionGroups` PRIMARY KEY (`CollectionId`, `GroupId`),
    CONSTRAINT `FK_CollectionGroups_Collection_CollectionId` FOREIGN KEY (`CollectionId`) REFERENCES `Collection` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_CollectionGroups_Group_GroupId` FOREIGN KEY (`GroupId`) REFERENCES `Group` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB;

CREATE TABLE `CollectionCipher` (
    `CollectionId` uuid NOT NULL,
    `CipherId` uuid NOT NULL,
    CONSTRAINT `PK_CollectionCipher` PRIMARY KEY (`CollectionId`, `CipherId`),
    CONSTRAINT `FK_CollectionCipher_Cipher_CipherId` FOREIGN KEY (`CipherId`) REFERENCES `Cipher` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_CollectionCipher_Collection_CollectionId` FOREIGN KEY (`CollectionId`) REFERENCES `Collection` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB;

CREATE TABLE `CollectionUsers` (
    `CollectionId` uuid NOT NULL,
    `OrganizationUserId` uuid NOT NULL,
    `UserId` uuid NULL,
    `ReadOnly` boolean NOT NULL,
    `HidePasswords` boolean NOT NULL,
    CONSTRAINT `PK_CollectionUsers` PRIMARY KEY (`CollectionId`, `OrganizationUserId`),
    CONSTRAINT `FK_CollectionUsers_Collection_CollectionId` FOREIGN KEY (`CollectionId`) REFERENCES `Collection` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_CollectionUsers_OrganizationUser_OrganizationUserId` FOREIGN KEY (`OrganizationUserId`) REFERENCES `OrganizationUser` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_CollectionUsers_User_UserId` FOREIGN KEY (`UserId`) REFERENCES `User` (`Id`) ON DELETE RESTRICT
) ENGINE=InnoDB;

CREATE TABLE `GroupUser` (
    `GroupId` uuid NOT NULL,
    `OrganizationUserId` uuid NOT NULL,
    `UserId` uuid NULL,
    CONSTRAINT `PK_GroupUser` PRIMARY KEY (`GroupId`, `OrganizationUserId`),
    CONSTRAINT `FK_GroupUser_Group_GroupId` FOREIGN KEY (`GroupId`) REFERENCES `Group` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_GroupUser_OrganizationUser_OrganizationUserId` FOREIGN KEY (`OrganizationUserId`) REFERENCES `OrganizationUser` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_GroupUser_User_UserId` FOREIGN KEY (`UserId`) REFERENCES `User` (`Id`) ON DELETE RESTRICT
) ENGINE=InnoDB;

CREATE TABLE `ProviderOrganizationProviderUser` (
    `Id` uuid NOT NULL,
    `ProviderOrganizationId` uuid NOT NULL,
    `ProviderUserId` uuid NOT NULL,
    `Type` smallint NOT NULL,
    `Permissions` text NULL,
    `CreationDate` timestamp without time zone NOT NULL,
    `RevisionDate` timestamp without time zone NOT NULL,
    CONSTRAINT `PK_ProviderOrganizationProviderUser` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_ProviderOrganizationProviderUser_ProviderOrganization_Provi~` FOREIGN KEY (`ProviderOrganizationId`) REFERENCES `ProviderOrganization` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_ProviderOrganizationProviderUser_ProviderUser_ProviderUserId` FOREIGN KEY (`ProviderUserId`) REFERENCES `ProviderUser` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB;

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
VALUES ('20210617143411_InitPostgres', '5.0.5');

COMMIT;

START TRANSACTION;

ALTER TABLE `ProviderOrganizationProviderUser` DROP FOREIGN KEY `FK_ProviderOrganizationProviderUser_ProviderOrganization_Provi~`;

ALTER TABLE `User` MODIFY COLUMN `TwoFactorRecoveryCode` varchar(32) CHARACTER SET utf8mb4 NULL;

ALTER TABLE `User` MODIFY COLUMN `TwoFactorProviders` longtext CHARACTER SET utf8mb4 NULL;

ALTER TABLE `User` MODIFY COLUMN `SecurityStamp` varchar(50) CHARACTER SET utf8mb4 NOT NULL;

ALTER TABLE `User` MODIFY COLUMN `RevisionDate` datetime(6) NOT NULL;

ALTER TABLE `User` MODIFY COLUMN `RenewalReminderDate` datetime(6) NULL;

ALTER TABLE `User` MODIFY COLUMN `ReferenceData` longtext CHARACTER SET utf8mb4 NULL;

ALTER TABLE `User` MODIFY COLUMN `PublicKey` longtext CHARACTER SET utf8mb4 NULL;

ALTER TABLE `User` MODIFY COLUMN `PrivateKey` longtext CHARACTER SET utf8mb4 NULL;

ALTER TABLE `User` MODIFY COLUMN `PremiumExpirationDate` datetime(6) NULL;

ALTER TABLE `User` MODIFY COLUMN `Premium` tinyint(1) NOT NULL;

ALTER TABLE `User` MODIFY COLUMN `Name` varchar(50) CHARACTER SET utf8mb4 NULL;

ALTER TABLE `User` MODIFY COLUMN `MasterPasswordHint` varchar(50) CHARACTER SET utf8mb4 NULL;

ALTER TABLE `User` MODIFY COLUMN `MasterPassword` varchar(300) CHARACTER SET utf8mb4 NULL;

ALTER TABLE `User` MODIFY COLUMN `LicenseKey` varchar(100) CHARACTER SET utf8mb4 NULL;

ALTER TABLE `User` MODIFY COLUMN `Key` longtext CHARACTER SET utf8mb4 NULL;

ALTER TABLE `User` MODIFY COLUMN `KdfIterations` int NOT NULL;

ALTER TABLE `User` MODIFY COLUMN `Kdf` tinyint unsigned NOT NULL;

ALTER TABLE `User` MODIFY COLUMN `GatewaySubscriptionId` varchar(50) CHARACTER SET utf8mb4 NULL;

ALTER TABLE `User` MODIFY COLUMN `GatewayCustomerId` varchar(50) CHARACTER SET utf8mb4 NULL;

ALTER TABLE `User` MODIFY COLUMN `Gateway` tinyint unsigned NULL;

ALTER TABLE `User` MODIFY COLUMN `ExcludedGlobalEquivalentDomains` longtext CHARACTER SET utf8mb4 NULL;

ALTER TABLE `User` MODIFY COLUMN `EquivalentDomains` longtext CHARACTER SET utf8mb4 NULL;

ALTER TABLE `User` MODIFY COLUMN `EmailVerified` tinyint(1) NOT NULL;

ALTER TABLE `User` MODIFY COLUMN `Email` varchar(256) CHARACTER SET utf8mb4 NOT NULL;

ALTER TABLE `User` MODIFY COLUMN `Culture` varchar(10) CHARACTER SET utf8mb4 NULL;

ALTER TABLE `User` MODIFY COLUMN `CreationDate` datetime(6) NOT NULL;

ALTER TABLE `User` MODIFY COLUMN `ApiKey` varchar(30) CHARACTER SET utf8mb4 NOT NULL;

ALTER TABLE `User` MODIFY COLUMN `AccountRevisionDate` datetime(6) NOT NULL;

ALTER TABLE `User` MODIFY COLUMN `Id` char(36) COLLATE ascii_general_ci NOT NULL;

ALTER TABLE `U2f` MODIFY COLUMN `Version` varchar(20) CHARACTER SET utf8mb4 NULL;

ALTER TABLE `U2f` MODIFY COLUMN `UserId` char(36) COLLATE ascii_general_ci NOT NULL;

ALTER TABLE `U2f` MODIFY COLUMN `KeyHandle` varchar(200) CHARACTER SET utf8mb4 NULL;

ALTER TABLE `U2f` MODIFY COLUMN `CreationDate` datetime(6) NOT NULL;

ALTER TABLE `U2f` MODIFY COLUMN `Challenge` varchar(200) CHARACTER SET utf8mb4 NULL;

ALTER TABLE `U2f` MODIFY COLUMN `AppId` varchar(50) CHARACTER SET utf8mb4 NULL;

ALTER TABLE `U2f` MODIFY COLUMN `Id` int NOT NULL AUTO_INCREMENT;

ALTER TABLE `Transaction` MODIFY COLUMN `UserId` char(36) COLLATE ascii_general_ci NULL;

ALTER TABLE `Transaction` MODIFY COLUMN `Type` tinyint unsigned NOT NULL;

ALTER TABLE `Transaction` MODIFY COLUMN `RefundedAmount` decimal(65,30) NULL;

ALTER TABLE `Transaction` MODIFY COLUMN `Refunded` tinyint(1) NULL;

ALTER TABLE `Transaction` MODIFY COLUMN `PaymentMethodType` tinyint unsigned NULL;

ALTER TABLE `Transaction` MODIFY COLUMN `OrganizationId` char(36) COLLATE ascii_general_ci NULL;

ALTER TABLE `Transaction` MODIFY COLUMN `GatewayId` varchar(50) CHARACTER SET utf8mb4 NULL;

ALTER TABLE `Transaction` MODIFY COLUMN `Gateway` tinyint unsigned NULL;

ALTER TABLE `Transaction` MODIFY COLUMN `Details` varchar(100) CHARACTER SET utf8mb4 NULL;

ALTER TABLE `Transaction` MODIFY COLUMN `CreationDate` datetime(6) NOT NULL;

ALTER TABLE `Transaction` MODIFY COLUMN `Amount` decimal(65,30) NOT NULL;

ALTER TABLE `Transaction` MODIFY COLUMN `Id` char(36) COLLATE ascii_general_ci NOT NULL;

ALTER TABLE `TaxRate` MODIFY COLUMN `State` varchar(2) CHARACTER SET utf8mb4 NULL;

ALTER TABLE `TaxRate` MODIFY COLUMN `Rate` decimal(65,30) NOT NULL;

ALTER TABLE `TaxRate` MODIFY COLUMN `PostalCode` varchar(10) CHARACTER SET utf8mb4 NULL;

ALTER TABLE `TaxRate` MODIFY COLUMN `Country` varchar(50) CHARACTER SET utf8mb4 NULL;

ALTER TABLE `TaxRate` MODIFY COLUMN `Active` tinyint(1) NOT NULL;

ALTER TABLE `TaxRate` MODIFY COLUMN `Id` varchar(40) CHARACTER SET utf8mb4 NOT NULL;

ALTER TABLE `SsoUser` MODIFY COLUMN `UserId` char(36) COLLATE ascii_general_ci NOT NULL;

ALTER TABLE `SsoUser` MODIFY COLUMN `OrganizationId` char(36) COLLATE ascii_general_ci NULL;

ALTER TABLE `SsoUser` MODIFY COLUMN `ExternalId` varchar(50) CHARACTER SET utf8mb4 NULL;

ALTER TABLE `SsoUser` MODIFY COLUMN `CreationDate` datetime(6) NOT NULL;

ALTER TABLE `SsoConfig` MODIFY COLUMN `RevisionDate` datetime(6) NOT NULL;

ALTER TABLE `SsoConfig` MODIFY COLUMN `OrganizationId` char(36) COLLATE ascii_general_ci NOT NULL;

ALTER TABLE `SsoConfig` MODIFY COLUMN `Enabled` tinyint(1) NOT NULL;

ALTER TABLE `SsoConfig` MODIFY COLUMN `Data` longtext CHARACTER SET utf8mb4 NULL;

ALTER TABLE `SsoConfig` MODIFY COLUMN `CreationDate` datetime(6) NOT NULL;

ALTER TABLE `Send` MODIFY COLUMN `UserId` char(36) COLLATE ascii_general_ci NULL;

ALTER TABLE `Send` MODIFY COLUMN `Type` tinyint unsigned NOT NULL;

ALTER TABLE `Send` MODIFY COLUMN `RevisionDate` datetime(6) NOT NULL;

ALTER TABLE `Send` MODIFY COLUMN `Password` varchar(300) CHARACTER SET utf8mb4 NULL;

ALTER TABLE `Send` MODIFY COLUMN `OrganizationId` char(36) COLLATE ascii_general_ci NULL;

ALTER TABLE `Send` MODIFY COLUMN `MaxAccessCount` int NULL;

ALTER TABLE `Send` MODIFY COLUMN `Key` longtext CHARACTER SET utf8mb4 NULL;

ALTER TABLE `Send` MODIFY COLUMN `HideEmail` tinyint(1) NULL;

ALTER TABLE `Send` MODIFY COLUMN `ExpirationDate` datetime(6) NULL;

ALTER TABLE `Send` MODIFY COLUMN `Disabled` tinyint(1) NOT NULL;

ALTER TABLE `Send` MODIFY COLUMN `DeletionDate` datetime(6) NOT NULL;

ALTER TABLE `Send` MODIFY COLUMN `Data` longtext CHARACTER SET utf8mb4 NULL;

ALTER TABLE `Send` MODIFY COLUMN `CreationDate` datetime(6) NOT NULL;

ALTER TABLE `Send` MODIFY COLUMN `AccessCount` int NOT NULL;

ALTER TABLE `Send` MODIFY COLUMN `Id` char(36) COLLATE ascii_general_ci NOT NULL;

ALTER TABLE `ProviderUser` MODIFY COLUMN `UserId` char(36) COLLATE ascii_general_ci NULL;

ALTER TABLE `ProviderUser` MODIFY COLUMN `Type` tinyint unsigned NOT NULL;

ALTER TABLE `ProviderUser` MODIFY COLUMN `Status` tinyint unsigned NOT NULL;

ALTER TABLE `ProviderUser` MODIFY COLUMN `RevisionDate` datetime(6) NOT NULL;

ALTER TABLE `ProviderUser` MODIFY COLUMN `ProviderId` char(36) COLLATE ascii_general_ci NOT NULL;

ALTER TABLE `ProviderUser` MODIFY COLUMN `Permissions` longtext CHARACTER SET utf8mb4 NULL;

ALTER TABLE `ProviderUser` MODIFY COLUMN `Key` longtext CHARACTER SET utf8mb4 NULL;

ALTER TABLE `ProviderUser` MODIFY COLUMN `Email` longtext CHARACTER SET utf8mb4 NULL;

ALTER TABLE `ProviderUser` MODIFY COLUMN `CreationDate` datetime(6) NOT NULL;

ALTER TABLE `ProviderUser` MODIFY COLUMN `Id` char(36) COLLATE ascii_general_ci NOT NULL;

ALTER TABLE `ProviderOrganizationProviderUser` MODIFY COLUMN `Type` tinyint unsigned NOT NULL;

ALTER TABLE `ProviderOrganizationProviderUser` MODIFY COLUMN `RevisionDate` datetime(6) NOT NULL;

ALTER TABLE `ProviderOrganizationProviderUser` MODIFY COLUMN `ProviderUserId` char(36) COLLATE ascii_general_ci NOT NULL;

ALTER TABLE `ProviderOrganizationProviderUser` MODIFY COLUMN `ProviderOrganizationId` char(36) COLLATE ascii_general_ci NOT NULL;

ALTER TABLE `ProviderOrganizationProviderUser` MODIFY COLUMN `Permissions` longtext CHARACTER SET utf8mb4 NULL;

ALTER TABLE `ProviderOrganizationProviderUser` MODIFY COLUMN `CreationDate` datetime(6) NOT NULL;

ALTER TABLE `ProviderOrganizationProviderUser` MODIFY COLUMN `Id` char(36) COLLATE ascii_general_ci NOT NULL;

ALTER TABLE `ProviderOrganization` MODIFY COLUMN `Settings` longtext CHARACTER SET utf8mb4 NULL;

ALTER TABLE `ProviderOrganization` MODIFY COLUMN `RevisionDate` datetime(6) NOT NULL;

ALTER TABLE `ProviderOrganization` MODIFY COLUMN `ProviderId` char(36) COLLATE ascii_general_ci NOT NULL;

ALTER TABLE `ProviderOrganization` MODIFY COLUMN `OrganizationId` char(36) COLLATE ascii_general_ci NOT NULL;

ALTER TABLE `ProviderOrganization` MODIFY COLUMN `Key` longtext CHARACTER SET utf8mb4 NULL;

ALTER TABLE `ProviderOrganization` MODIFY COLUMN `CreationDate` datetime(6) NOT NULL;

ALTER TABLE `ProviderOrganization` MODIFY COLUMN `Id` char(36) COLLATE ascii_general_ci NOT NULL;

ALTER TABLE `Provider` MODIFY COLUMN `Status` tinyint unsigned NOT NULL;

ALTER TABLE `Provider` MODIFY COLUMN `RevisionDate` datetime(6) NOT NULL;

ALTER TABLE `Provider` MODIFY COLUMN `Name` longtext CHARACTER SET utf8mb4 NULL;

ALTER TABLE `Provider` MODIFY COLUMN `Enabled` tinyint(1) NOT NULL;

ALTER TABLE `Provider` MODIFY COLUMN `CreationDate` datetime(6) NOT NULL;

ALTER TABLE `Provider` MODIFY COLUMN `BusinessTaxNumber` longtext CHARACTER SET utf8mb4 NULL;

ALTER TABLE `Provider` MODIFY COLUMN `BusinessName` longtext CHARACTER SET utf8mb4 NULL;

ALTER TABLE `Provider` MODIFY COLUMN `BusinessCountry` longtext CHARACTER SET utf8mb4 NULL;

ALTER TABLE `Provider` MODIFY COLUMN `BusinessAddress3` longtext CHARACTER SET utf8mb4 NULL;

ALTER TABLE `Provider` MODIFY COLUMN `BusinessAddress2` longtext CHARACTER SET utf8mb4 NULL;

ALTER TABLE `Provider` MODIFY COLUMN `BusinessAddress1` longtext CHARACTER SET utf8mb4 NULL;

ALTER TABLE `Provider` MODIFY COLUMN `BillingEmail` longtext CHARACTER SET utf8mb4 NULL;

ALTER TABLE `Provider` MODIFY COLUMN `Id` char(36) COLLATE ascii_general_ci NOT NULL;

ALTER TABLE `Policy` MODIFY COLUMN `Type` tinyint unsigned NOT NULL;

ALTER TABLE `Policy` MODIFY COLUMN `RevisionDate` datetime(6) NOT NULL;

ALTER TABLE `Policy` MODIFY COLUMN `OrganizationId` char(36) COLLATE ascii_general_ci NOT NULL;

ALTER TABLE `Policy` MODIFY COLUMN `Enabled` tinyint(1) NOT NULL;

ALTER TABLE `Policy` MODIFY COLUMN `Data` longtext CHARACTER SET utf8mb4 NULL;

ALTER TABLE `Policy` MODIFY COLUMN `CreationDate` datetime(6) NOT NULL;

ALTER TABLE `Policy` MODIFY COLUMN `Id` char(36) COLLATE ascii_general_ci NOT NULL;

ALTER TABLE `OrganizationUser` MODIFY COLUMN `UserId` char(36) COLLATE ascii_general_ci NULL;

ALTER TABLE `OrganizationUser` MODIFY COLUMN `Type` tinyint unsigned NOT NULL;

ALTER TABLE `OrganizationUser` MODIFY COLUMN `Status` tinyint unsigned NOT NULL;

ALTER TABLE `OrganizationUser` MODIFY COLUMN `RevisionDate` datetime(6) NOT NULL;

ALTER TABLE `OrganizationUser` MODIFY COLUMN `ResetPasswordKey` longtext CHARACTER SET utf8mb4 NULL;

ALTER TABLE `OrganizationUser` MODIFY COLUMN `Permissions` longtext CHARACTER SET utf8mb4 NULL;

ALTER TABLE `OrganizationUser` MODIFY COLUMN `OrganizationId` char(36) COLLATE ascii_general_ci NOT NULL;

ALTER TABLE `OrganizationUser` MODIFY COLUMN `Key` longtext CHARACTER SET utf8mb4 NULL;

ALTER TABLE `OrganizationUser` MODIFY COLUMN `ExternalId` varchar(300) CHARACTER SET utf8mb4 NULL;

ALTER TABLE `OrganizationUser` MODIFY COLUMN `Email` varchar(256) CHARACTER SET utf8mb4 NULL;

ALTER TABLE `OrganizationUser` MODIFY COLUMN `CreationDate` datetime(6) NOT NULL;

ALTER TABLE `OrganizationUser` MODIFY COLUMN `AccessAll` tinyint(1) NOT NULL;

ALTER TABLE `OrganizationUser` MODIFY COLUMN `Id` char(36) COLLATE ascii_general_ci NOT NULL;

ALTER TABLE `Organization` MODIFY COLUMN `UsersGetPremium` tinyint(1) NOT NULL;

ALTER TABLE `Organization` MODIFY COLUMN `UseTotp` tinyint(1) NOT NULL;

ALTER TABLE `Organization` MODIFY COLUMN `UseSso` tinyint(1) NOT NULL;

ALTER TABLE `Organization` MODIFY COLUMN `UseResetPassword` tinyint(1) NOT NULL;

ALTER TABLE `Organization` MODIFY COLUMN `UsePolicies` tinyint(1) NOT NULL;

ALTER TABLE `Organization` MODIFY COLUMN `UseGroups` tinyint(1) NOT NULL;

ALTER TABLE `Organization` MODIFY COLUMN `UseEvents` tinyint(1) NOT NULL;

ALTER TABLE `Organization` MODIFY COLUMN `UseDirectory` tinyint(1) NOT NULL;

ALTER TABLE `Organization` MODIFY COLUMN `UseApi` tinyint(1) NOT NULL;

ALTER TABLE `Organization` MODIFY COLUMN `Use2fa` tinyint(1) NOT NULL;

ALTER TABLE `Organization` MODIFY COLUMN `TwoFactorProviders` longtext CHARACTER SET utf8mb4 NULL;

ALTER TABLE `Organization` MODIFY COLUMN `SelfHost` tinyint(1) NOT NULL;

ALTER TABLE `Organization` MODIFY COLUMN `Seats` int NULL;

ALTER TABLE `Organization` MODIFY COLUMN `RevisionDate` datetime(6) NOT NULL;

ALTER TABLE `Organization` MODIFY COLUMN `ReferenceData` longtext CHARACTER SET utf8mb4 NULL;

ALTER TABLE `Organization` MODIFY COLUMN `PublicKey` longtext CHARACTER SET utf8mb4 NULL;

ALTER TABLE `Organization` MODIFY COLUMN `PrivateKey` longtext CHARACTER SET utf8mb4 NULL;

ALTER TABLE `Organization` MODIFY COLUMN `PlanType` tinyint unsigned NOT NULL;

ALTER TABLE `Organization` MODIFY COLUMN `Plan` varchar(50) CHARACTER SET utf8mb4 NULL;

ALTER TABLE `Organization` MODIFY COLUMN `Name` varchar(50) CHARACTER SET utf8mb4 NULL;

ALTER TABLE `Organization` MODIFY COLUMN `LicenseKey` varchar(100) CHARACTER SET utf8mb4 NULL;

ALTER TABLE `Organization` MODIFY COLUMN `Identifier` varchar(50) CHARACTER SET utf8mb4 NULL;

ALTER TABLE `Organization` MODIFY COLUMN `GatewaySubscriptionId` varchar(50) CHARACTER SET utf8mb4 NULL;

ALTER TABLE `Organization` MODIFY COLUMN `GatewayCustomerId` varchar(50) CHARACTER SET utf8mb4 NULL;

ALTER TABLE `Organization` MODIFY COLUMN `Gateway` tinyint unsigned NULL;

ALTER TABLE `Organization` MODIFY COLUMN `ExpirationDate` datetime(6) NULL;

ALTER TABLE `Organization` MODIFY COLUMN `Enabled` tinyint(1) NOT NULL;

ALTER TABLE `Organization` MODIFY COLUMN `CreationDate` datetime(6) NOT NULL;

ALTER TABLE `Organization` MODIFY COLUMN `BusinessTaxNumber` varchar(30) CHARACTER SET utf8mb4 NULL;

ALTER TABLE `Organization` MODIFY COLUMN `BusinessName` varchar(50) CHARACTER SET utf8mb4 NULL;

ALTER TABLE `Organization` MODIFY COLUMN `BusinessCountry` varchar(2) CHARACTER SET utf8mb4 NULL;

ALTER TABLE `Organization` MODIFY COLUMN `BusinessAddress3` varchar(50) CHARACTER SET utf8mb4 NULL;

ALTER TABLE `Organization` MODIFY COLUMN `BusinessAddress2` varchar(50) CHARACTER SET utf8mb4 NULL;

ALTER TABLE `Organization` MODIFY COLUMN `BusinessAddress1` varchar(50) CHARACTER SET utf8mb4 NULL;

ALTER TABLE `Organization` MODIFY COLUMN `BillingEmail` varchar(256) CHARACTER SET utf8mb4 NULL;

ALTER TABLE `Organization` MODIFY COLUMN `ApiKey` varchar(30) CHARACTER SET utf8mb4 NULL;

ALTER TABLE `Organization` MODIFY COLUMN `Id` char(36) COLLATE ascii_general_ci NOT NULL;

ALTER TABLE `Installation` MODIFY COLUMN `Key` varchar(150) CHARACTER SET utf8mb4 NULL;

ALTER TABLE `Installation` MODIFY COLUMN `Enabled` tinyint(1) NOT NULL;

ALTER TABLE `Installation` MODIFY COLUMN `Email` varchar(256) CHARACTER SET utf8mb4 NULL;

ALTER TABLE `Installation` MODIFY COLUMN `CreationDate` datetime(6) NOT NULL;

ALTER TABLE `Installation` MODIFY COLUMN `Id` char(36) COLLATE ascii_general_ci NOT NULL;

ALTER TABLE `GroupUser` MODIFY COLUMN `UserId` char(36) COLLATE ascii_general_ci NULL;

ALTER TABLE `GroupUser` MODIFY COLUMN `OrganizationUserId` char(36) COLLATE ascii_general_ci NOT NULL;

ALTER TABLE `GroupUser` MODIFY COLUMN `GroupId` char(36) COLLATE ascii_general_ci NOT NULL;

ALTER TABLE `Group` MODIFY COLUMN `RevisionDate` datetime(6) NOT NULL;

ALTER TABLE `Group` MODIFY COLUMN `OrganizationId` char(36) COLLATE ascii_general_ci NOT NULL;

ALTER TABLE `Group` MODIFY COLUMN `Name` varchar(100) CHARACTER SET utf8mb4 NULL;

ALTER TABLE `Group` MODIFY COLUMN `ExternalId` varchar(300) CHARACTER SET utf8mb4 NULL;

ALTER TABLE `Group` MODIFY COLUMN `CreationDate` datetime(6) NOT NULL;

ALTER TABLE `Group` MODIFY COLUMN `AccessAll` tinyint(1) NOT NULL;

ALTER TABLE `Group` MODIFY COLUMN `Id` char(36) COLLATE ascii_general_ci NOT NULL;

ALTER TABLE `Grant` MODIFY COLUMN `Type` varchar(50) CHARACTER SET utf8mb4 NULL;

ALTER TABLE `Grant` MODIFY COLUMN `SubjectId` varchar(200) CHARACTER SET utf8mb4 NULL;

ALTER TABLE `Grant` MODIFY COLUMN `SessionId` varchar(100) CHARACTER SET utf8mb4 NULL;

ALTER TABLE `Grant` MODIFY COLUMN `ExpirationDate` datetime(6) NULL;

ALTER TABLE `Grant` MODIFY COLUMN `Description` varchar(200) CHARACTER SET utf8mb4 NULL;

ALTER TABLE `Grant` MODIFY COLUMN `Data` longtext CHARACTER SET utf8mb4 NULL;

ALTER TABLE `Grant` MODIFY COLUMN `CreationDate` datetime(6) NOT NULL;

ALTER TABLE `Grant` MODIFY COLUMN `ConsumedDate` datetime(6) NULL;

ALTER TABLE `Grant` MODIFY COLUMN `ClientId` varchar(200) CHARACTER SET utf8mb4 NULL;

ALTER TABLE `Grant` MODIFY COLUMN `Key` varchar(200) CHARACTER SET utf8mb4 NOT NULL;

ALTER TABLE `Folder` MODIFY COLUMN `UserId` char(36) COLLATE ascii_general_ci NOT NULL;

ALTER TABLE `Folder` MODIFY COLUMN `RevisionDate` datetime(6) NOT NULL;

ALTER TABLE `Folder` MODIFY COLUMN `Name` longtext CHARACTER SET utf8mb4 NULL;

ALTER TABLE `Folder` MODIFY COLUMN `CreationDate` datetime(6) NOT NULL;

ALTER TABLE `Folder` MODIFY COLUMN `Id` char(36) COLLATE ascii_general_ci NOT NULL;

ALTER TABLE `Event` MODIFY COLUMN `UserId` char(36) COLLATE ascii_general_ci NULL;

ALTER TABLE `Event` MODIFY COLUMN `Type` int NOT NULL;

ALTER TABLE `Event` MODIFY COLUMN `PolicyId` char(36) COLLATE ascii_general_ci NULL;

ALTER TABLE `Event` MODIFY COLUMN `OrganizationUserId` char(36) COLLATE ascii_general_ci NULL;

ALTER TABLE `Event` MODIFY COLUMN `OrganizationId` char(36) COLLATE ascii_general_ci NULL;

ALTER TABLE `Event` MODIFY COLUMN `IpAddress` varchar(50) CHARACTER SET utf8mb4 NULL;

ALTER TABLE `Event` MODIFY COLUMN `GroupId` char(36) COLLATE ascii_general_ci NULL;

ALTER TABLE `Event` MODIFY COLUMN `DeviceType` tinyint unsigned NULL;

ALTER TABLE `Event` MODIFY COLUMN `Date` datetime(6) NOT NULL;

ALTER TABLE `Event` MODIFY COLUMN `CollectionId` char(36) COLLATE ascii_general_ci NULL;

ALTER TABLE `Event` MODIFY COLUMN `CipherId` char(36) COLLATE ascii_general_ci NULL;

ALTER TABLE `Event` MODIFY COLUMN `ActingUserId` char(36) COLLATE ascii_general_ci NULL;

ALTER TABLE `Event` MODIFY COLUMN `Id` char(36) COLLATE ascii_general_ci NOT NULL;

ALTER TABLE `EmergencyAccess` MODIFY COLUMN `WaitTimeDays` int NOT NULL;

ALTER TABLE `EmergencyAccess` MODIFY COLUMN `Type` tinyint unsigned NOT NULL;

ALTER TABLE `EmergencyAccess` MODIFY COLUMN `Status` tinyint unsigned NOT NULL;

ALTER TABLE `EmergencyAccess` MODIFY COLUMN `RevisionDate` datetime(6) NOT NULL;

ALTER TABLE `EmergencyAccess` MODIFY COLUMN `RecoveryInitiatedDate` datetime(6) NULL;

ALTER TABLE `EmergencyAccess` MODIFY COLUMN `LastNotificationDate` datetime(6) NULL;

ALTER TABLE `EmergencyAccess` MODIFY COLUMN `KeyEncrypted` longtext CHARACTER SET utf8mb4 NULL;

ALTER TABLE `EmergencyAccess` MODIFY COLUMN `GrantorId` char(36) COLLATE ascii_general_ci NOT NULL;

ALTER TABLE `EmergencyAccess` MODIFY COLUMN `GranteeId` char(36) COLLATE ascii_general_ci NULL;

ALTER TABLE `EmergencyAccess` MODIFY COLUMN `Email` varchar(256) CHARACTER SET utf8mb4 NULL;

ALTER TABLE `EmergencyAccess` MODIFY COLUMN `CreationDate` datetime(6) NOT NULL;

ALTER TABLE `EmergencyAccess` MODIFY COLUMN `Id` char(36) COLLATE ascii_general_ci NOT NULL;

ALTER TABLE `Device` MODIFY COLUMN `UserId` char(36) COLLATE ascii_general_ci NOT NULL;

ALTER TABLE `Device` MODIFY COLUMN `Type` tinyint unsigned NOT NULL;

ALTER TABLE `Device` MODIFY COLUMN `RevisionDate` datetime(6) NOT NULL;

ALTER TABLE `Device` MODIFY COLUMN `PushToken` varchar(255) CHARACTER SET utf8mb4 NULL;

ALTER TABLE `Device` MODIFY COLUMN `Name` varchar(50) CHARACTER SET utf8mb4 NULL;

ALTER TABLE `Device` MODIFY COLUMN `Identifier` varchar(50) CHARACTER SET utf8mb4 NULL;

ALTER TABLE `Device` MODIFY COLUMN `CreationDate` datetime(6) NOT NULL;

ALTER TABLE `Device` MODIFY COLUMN `Id` char(36) COLLATE ascii_general_ci NOT NULL;

ALTER TABLE `CollectionUsers` MODIFY COLUMN `UserId` char(36) COLLATE ascii_general_ci NULL;

ALTER TABLE `CollectionUsers` MODIFY COLUMN `ReadOnly` tinyint(1) NOT NULL;

ALTER TABLE `CollectionUsers` MODIFY COLUMN `HidePasswords` tinyint(1) NOT NULL;

ALTER TABLE `CollectionUsers` MODIFY COLUMN `OrganizationUserId` char(36) COLLATE ascii_general_ci NOT NULL;

ALTER TABLE `CollectionUsers` MODIFY COLUMN `CollectionId` char(36) COLLATE ascii_general_ci NOT NULL;

ALTER TABLE `CollectionGroups` MODIFY COLUMN `ReadOnly` tinyint(1) NOT NULL;

ALTER TABLE `CollectionGroups` MODIFY COLUMN `HidePasswords` tinyint(1) NOT NULL;

ALTER TABLE `CollectionGroups` MODIFY COLUMN `GroupId` char(36) COLLATE ascii_general_ci NOT NULL;

ALTER TABLE `CollectionGroups` MODIFY COLUMN `CollectionId` char(36) COLLATE ascii_general_ci NOT NULL;

ALTER TABLE `CollectionCipher` MODIFY COLUMN `CipherId` char(36) COLLATE ascii_general_ci NOT NULL;

ALTER TABLE `CollectionCipher` MODIFY COLUMN `CollectionId` char(36) COLLATE ascii_general_ci NOT NULL;

ALTER TABLE `Collection` MODIFY COLUMN `RevisionDate` datetime(6) NOT NULL;

ALTER TABLE `Collection` MODIFY COLUMN `OrganizationId` char(36) COLLATE ascii_general_ci NOT NULL;

ALTER TABLE `Collection` MODIFY COLUMN `Name` longtext CHARACTER SET utf8mb4 NULL;

ALTER TABLE `Collection` MODIFY COLUMN `ExternalId` varchar(300) CHARACTER SET utf8mb4 NULL;

ALTER TABLE `Collection` MODIFY COLUMN `CreationDate` datetime(6) NOT NULL;

ALTER TABLE `Collection` MODIFY COLUMN `Id` char(36) COLLATE ascii_general_ci NOT NULL;

ALTER TABLE `Cipher` MODIFY COLUMN `UserId` char(36) COLLATE ascii_general_ci NULL;

ALTER TABLE `Cipher` MODIFY COLUMN `Type` tinyint unsigned NOT NULL;

ALTER TABLE `Cipher` MODIFY COLUMN `RevisionDate` datetime(6) NOT NULL;

ALTER TABLE `Cipher` MODIFY COLUMN `Reprompt` tinyint unsigned NULL;

ALTER TABLE `Cipher` MODIFY COLUMN `OrganizationId` char(36) COLLATE ascii_general_ci NULL;

ALTER TABLE `Cipher` MODIFY COLUMN `Folders` longtext CHARACTER SET utf8mb4 NULL;

ALTER TABLE `Cipher` MODIFY COLUMN `Favorites` longtext CHARACTER SET utf8mb4 NULL;

ALTER TABLE `Cipher` MODIFY COLUMN `DeletedDate` datetime(6) NULL;

ALTER TABLE `Cipher` MODIFY COLUMN `Data` longtext CHARACTER SET utf8mb4 NULL;

ALTER TABLE `Cipher` MODIFY COLUMN `CreationDate` datetime(6) NOT NULL;

ALTER TABLE `Cipher` MODIFY COLUMN `Attachments` longtext CHARACTER SET utf8mb4 NULL;

ALTER TABLE `Cipher` MODIFY COLUMN `Id` char(36) COLLATE ascii_general_ci NOT NULL;

ALTER TABLE `ProviderOrganizationProviderUser` ADD CONSTRAINT `FK_ProviderOrganizationProviderUser_ProviderOrganization_Provid~` FOREIGN KEY (`ProviderOrganizationId`) REFERENCES `ProviderOrganization` (`Id`) ON DELETE CASCADE;

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20210617152444_InitMySql', '5.0.5');

COMMIT;
