 -- Stored procedure that filters out ciphers that ONLY belong to default collections
CREATE PROCEDURE
  [dbo].[CipherOrganizationDetails_ReadByOrganizationIdExcludingDefaultCollections]
      @OrganizationId UNIQUEIDENTIFIER
  AS
  BEGIN
      SET NOCOUNT ON;

      WITH [NonDefaultCiphers] AS (
          SELECT DISTINCT [Id]
          FROM [dbo].[OrganizationCipherDetailsCollectionsView]
          WHERE [OrganizationId] = @OrganizationId
              AND ([CollectionId] IS NULL
              OR [CollectionType] <> 1)
      )

      SELECT
          V.[Id],
          V.[UserId],
          V.[OrganizationId],
          V.[Type],
          V.[Data],
          V.[Favorites],
          V.[Folders],
          V.[Attachments],
          V.[CreationDate],
          V.[RevisionDate],
          V.[DeletedDate],
          V.[Reprompt],
          V.[Key],
          V.[OrganizationUseTotp],
          V.[CollectionId]  -- For Dapper splitOn parameter
      FROM [dbo].[OrganizationCipherDetailsCollectionsView] V
      INNER JOIN [NonDefaultCiphers] NDC ON V.[Id] = NDC.[Id]
      WHERE V.[OrganizationId] = @OrganizationId
          AND (V.[CollectionId] IS NULL OR V.[CollectionType] <> 1)
      ORDER BY V.[RevisionDate] DESC;
  END;
  GO