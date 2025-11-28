CREATE VIEW [dbo].[OrganizationCipherDetailsCollectionsView]
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
