BEGIN TRANSACTION;

ALTER TABLE "User" ADD "AvatarColor" TEXT NULL;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20230103230301_AddAvatarColor', '6.0.12');

COMMIT;