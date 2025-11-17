CREATE PROCEDURE [dbo].[CipherDetails_ReadWithoutOrganizationsByUserId]
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
        1 [Edit],
        1 [ViewPassword],
        1 [Manage],
        0 [OrganizationUseTotp],
        ca.ArchivedDate
    FROM
        [dbo].[CipherDetails](@UserId) AS c
        LEFT JOIN [dbo].[CipherArchive] AS ca
            ON ca.CipherId = c.Id
           AND ca.UserId = @UserId
    WHERE
        c.[UserId] = @UserId
END