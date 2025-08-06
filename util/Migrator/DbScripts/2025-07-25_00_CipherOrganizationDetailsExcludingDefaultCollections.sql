-- View that provides organization cipher details with their collection associations
CREATE OR ALTER VIEW [dbo].[OrganizationCipherDetailsWithCollectionsView]
AS
    SELECT
      C.[Id],
      C.[UserId],
      C.[OrganizationId],
      C.[Type],
      C.[Data],
      C.[Attachments],
      C.[Favorites],
      C.[Folders],
      C.[CreationDate],
      C.[RevisionDate],
      C.[DeletedDate],
      C.[Reprompt],
      C.[Key],
      CASE
          WHEN O.[UseTotp] = 1 THEN 1
          ELSE 0
      END AS [OrganizationUseTotp],
      CC.[CollectionId],
      COL.[Type] AS [CollectionType]
    FROM [dbo].[Cipher] C
    INNER JOIN [dbo].[Organization] O ON C.[OrganizationId] = O.[Id]
    LEFT JOIN [dbo].[CollectionCipher] CC ON CC.[CipherId] = C.[Id]
    LEFT JOIN [dbo].[Collection] COL ON CC.[CollectionId] = COL.[Id]
    WHERE C.[UserId] IS NULL -- Organization ciphers only
      AND O.[Enabled] = 1; -- Only enabled organizations
GO

 -- Stored procedure that filters out ciphers that ONLY belong to default collections
CREATE OR ALTER PROCEDURE
  [dbo].[CipherOrganizationDetails_ReadByOrganizationIdExcludingDefaultCollections]
      @OrganizationId UNIQUEIDENTIFIER
  AS
  BEGIN
      SET NOCOUNT ON;

      WITH [NonDefaultCiphers] AS (
          SELECT DISTINCT [Id]
          FROM [dbo].[OrganizationCipherDetailsWithCollectionsView]
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
      FROM [dbo].[OrganizationCipherDetailsWithCollectionsView] V
      INNER JOIN [NonDefaultCiphers] NDC ON V.[Id] = NDC.[Id]
      WHERE V.[OrganizationId] = @OrganizationId
      ORDER BY V.[RevisionDate] DESC;
  END;
  GO

CREATE NONCLUSTERED INDEX IX_Cipher_OrganizationId_Filtered_OrgCiphersOnly
      ON [dbo].[Cipher] ([OrganizationId])
      INCLUDE ([Id], [Type], [Data], [Favorites], [Folders], [Attachments], [CreationDate],
[RevisionDate], [DeletedDate], [Reprompt], [Key])
      WHERE [UserId] IS NULL;
