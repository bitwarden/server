START TRANSACTION;

CREATE TABLE `ProjectSecret` (
    `ProjectsId` char(36) COLLATE ascii_general_ci NOT NULL,
    `SecretsId` char(36) COLLATE ascii_general_ci NOT NULL,
    CONSTRAINT `PK_ProjectSecret` PRIMARY KEY (`ProjectsId`, `SecretsId`),
    CONSTRAINT `FK_ProjectSecret_Project_ProjectsId` FOREIGN KEY (`ProjectsId`) REFERENCES `Project` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_ProjectSecret_Secret_SecretsId` FOREIGN KEY (`SecretsId`) REFERENCES `Secret` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE INDEX `IX_ProjectSecret_SecretsId` ON `ProjectSecret` (`SecretsId`);

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20221018133046_ProjectSecret', '6.0.4');

COMMIT;
