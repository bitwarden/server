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
    WHERE C.[UserId] IS NULL; -- Organization ciphers only
GO
 -- Stored procedure that filters out ciphers that ONLY belong to default collections
CREATE OR ALTER PROCEDURE [dbo].[CipherOrganizationDetails_ReadByOrganizationIdExcludingDefaultCollections]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        C.[Id],
        C.[UserId],
        C.[OrganizationId],
        C.[Type],
        C.[Data],
        C.[Attachments],
        C.[CreationDate],
        C.[RevisionDate],
        C.[DeletedDate],
        C.[Reprompt],
        C.[Key],
        CASE WHEN O.[UseTotp] = 1 THEN 1 ELSE 0 END AS [OrganizationUseTotp],
        CC.[CollectionId]
    FROM [dbo].[Cipher] C
    INNER JOIN [dbo].[Organization] O
        ON C.[OrganizationId] = O.[Id]
    LEFT JOIN [dbo].[CollectionCipher] CC
        ON CC.[CipherId] = C.[Id]
    LEFT JOIN [dbo].[Collection] COL
        ON CC.[CollectionId] = COL.[Id]
    WHERE
        C.[UserId]       IS NULL                              -- only org-owned ciphers
        AND C.[OrganizationId] = @OrganizationId
        AND (CC.[CollectionId] IS NULL                        -- ciphers with no collections
             OR COL.[Type]       <> 1);                       -- or non-default collections
END
GO

GO
CREATE NONCLUSTERED INDEX IX_Cipher_OrganizationId_Filtered_OrgCiphersOnly
    ON [dbo].[Cipher] ([OrganizationId])
    INCLUDE ([Id], [Type], [Data], [Attachments], [CreationDate], [RevisionDate], [DeletedDate], [Reprompt], [Key])
    WHERE [UserId] IS NULL;
