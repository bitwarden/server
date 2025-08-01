 -- Stored procedure that filters out ciphers that ONLY belong to default collections
CREATE PROCEDURE [dbo].[CipherOrganizationDetails_ReadByOrganizationIdExcludingDefaultCollections]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        [Id],
        [UserId],
        [OrganizationId],
        [Type],
        [Data],
        [Attachments],
        [CreationDate],
        [RevisionDate],
        [DeletedDate],
        [Reprompt],
        [Key],
        [OrganizationUseTotp],
        [CollectionId]
    FROM dbo.OrganizationCipherDetailsWithCollectionsView
    WHERE [OrganizationId] = @OrganizationId
      AND ([CollectionId] IS NULL       -- no collections
           OR [CollectionType] <> 1);  -- or at least one non-default
END;
GO