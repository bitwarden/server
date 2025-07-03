CREATE PROCEDURE [dbo].[CipherOrganizationDetails_ReadByOrganizationIdExcludingDefaultCollections]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    -- Ciphers with no collections
    SELECT DISTINCT
        [Id],w
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
        [OrganizationUseTotp]
    FROM [dbo].[OrganizationCipherDetailsWithCollectionsView]
    WHERE [OrganizationId] = @OrganizationId
        AND [CollectionId] IS NULL

    UNION

    -- Ciphers with at least one non-default collection
    SELECT DISTINCT
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
        [OrganizationUseTotp]
    FROM [dbo].[OrganizationCipherDetailsWithCollectionsView]
    WHERE [OrganizationId] = @OrganizationId
        AND ([CollectionType] IS NULL OR [CollectionType] != 1);
END
GO
