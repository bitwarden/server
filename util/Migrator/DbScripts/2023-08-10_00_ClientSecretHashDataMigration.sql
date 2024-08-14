/*
This is the data migration script for the client secret hash updates.
The initial migration util/Migrator/DbScripts/2023-05-16_00_ClientSecretHash.sql should be run prior.
The final migration is in util/Migrator/DbScripts/2023-08-10_01_RemoveClientSecret
*/
IF COL_LENGTH('[dbo].[ApiKey]', 'ClientSecretHash') IS NOT NULL AND COL_LENGTH('[dbo].[ApiKey]', 'ClientSecret')  IS NOT NULL
BEGIN

  -- Data Migration
  DECLARE @BatchSize INT = 10000
  WHILE @BatchSize > 0
  BEGIN
    BEGIN TRANSACTION Migrate_ClientSecretHash

    UPDATE TOP(@BatchSize) [dbo].[ApiKey]
    SET ClientSecretHash = (
        SELECT CAST(N'' AS XML).value('xs:base64Binary(sql:column("HASH"))', 'VARCHAR(128)')
        FROM (
            SELECT HASHBYTES('SHA2_256', [ClientSecret]) AS HASH
            ) SRC
        )
    WHERE [ClientSecretHash] IS NULL

    SET @BatchSize = @@ROWCOUNT

    COMMIT TRANSACTION Migrate_ClientSecretHash
  END

END
GO
