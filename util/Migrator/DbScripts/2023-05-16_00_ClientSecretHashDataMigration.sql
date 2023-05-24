IF COL_LENGTH('[dbo].[ApiKey]', 'ClientSecretHash') IS NOT NULL AND COL_LENGTH('[dbo].[ApiKey]', 'ClientSecret')  IS NOT NULL
BEGIN

  -- Add index
  IF NOT EXISTS(SELECT name FROM sys.indexes WHERE name = 'IX_ApiKey_ClientSecretHash')
  BEGIN
   CREATE NONCLUSTERED INDEX [IX_ApiKey_ClientSecretHash] 
   ON [dbo].[ApiKey]([ClientSecretHash] ASC)
   WITH (ONLINE = ON)
  END

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

  -- Drop index
  DROP INDEX IF EXISTS [IX_ApiKey_ClientSecretHash]
      ON [dbo].[ApiKey];

END
GO
