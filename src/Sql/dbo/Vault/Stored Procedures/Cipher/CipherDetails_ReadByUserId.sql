CREATE PROCEDURE [dbo].[CipherDetails_ReadByUserId]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        c.Id,
        c.UserId,
        c.OrganizationId,
        c.Type,
        c.Data,
        c.Favorites,
        c.Folders,
        c.Attachments,
        c.CreationDate,
        c.RevisionDate,
        c.DeletedDate,
        c.Reprompt,
        c.Key,
        ca.ArchivedDate
    FROM
        [dbo].[UserCipherDetails](@UserId) AS c
        LEFT JOIN [dbo].[CipherArchive] AS ca
            ON ca.CipherId = c.Id
           AND ca.UserId = @UserId
END