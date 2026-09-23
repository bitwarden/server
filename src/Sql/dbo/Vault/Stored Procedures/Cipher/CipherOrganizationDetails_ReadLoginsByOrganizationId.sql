CREATE PROCEDURE [dbo].[CipherOrganizationDetails_ReadLoginsByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

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
    WHERE V.[OrganizationId] = @OrganizationId
        AND V.[Type] = 1
    ORDER BY V.[RevisionDate] DESC;
END;
