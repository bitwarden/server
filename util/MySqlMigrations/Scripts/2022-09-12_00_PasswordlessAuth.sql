START TRANSACTION;

CREATE TABLE `AuthRequest` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `UserId` char(36) COLLATE ascii_general_ci NOT NULL,
    `Type` tinyint unsigned NOT NULL,
    `RequestDeviceIdentifier` varchar(50) CHARACTER SET utf8mb4 NULL,
    `RequestDeviceType` tinyint unsigned NOT NULL,
    `RequestIpAddress` varchar(50) CHARACTER SET utf8mb4 NULL,
    `RequestFingerprint` longtext CHARACTER SET utf8mb4 NULL,
    `ResponseDeviceId` char(36) COLLATE ascii_general_ci NULL,
    `AccessCode` varchar(25) CHARACTER SET utf8mb4 NULL,
    `PublicKey` longtext CHARACTER SET utf8mb4 NULL,
    `Key` longtext CHARACTER SET utf8mb4 NULL,
    `MasterPasswordHash` longtext CHARACTER SET utf8mb4 NULL,
    `CreationDate` datetime(6) NOT NULL,
    `ResponseDate` datetime(6) NULL,
    `AuthenticationDate` datetime(6) NULL,
    CONSTRAINT `PK_AuthRequest` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_AuthRequest_Device_ResponseDeviceId` FOREIGN KEY (`ResponseDeviceId`) REFERENCES `Device` (`Id`),
    CONSTRAINT `FK_AuthRequest_User_UserId` FOREIGN KEY (`UserId`) REFERENCES `User` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE INDEX `IX_AuthRequest_ResponseDeviceId` ON `AuthRequest` (`ResponseDeviceId`);

CREATE INDEX `IX_AuthRequest_UserId` ON `AuthRequest` (`UserId`);

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20220912144222_PasswordlessAuthRequests', '6.0.4');

COMMIT;
