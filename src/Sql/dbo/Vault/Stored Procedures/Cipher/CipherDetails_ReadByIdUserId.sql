CREATE PROCEDURE [dbo].[CipherDetails_ReadByIdUserId]
    @Id UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        ucd.Id,
        ucd.UserId,
        ucd.OrganizationId,
        ucd.Type,
        ucd.Data,
        ucd.Attachments,
        ucd.CreationDate,
        ucd.RevisionDate,
        ucd.Favorite,
        ucd.FolderId,
        ucd.DeletedDate,
        ucd.Reprompt,
        ucd.[Key],
        ucd.OrganizationUseTotp,
        MAX(ca.ArchivedDate) AS ArchivedDate,
        MAX(ucd.Edit) AS Edit,
        MAX(ucd.ViewPassword) AS ViewPassword,
        MAX(ucd.Manage) AS Manage
    FROM
        [dbo].[UserCipherDetails](@UserId) AS ucd
        LEFT JOIN [dbo].[CipherArchive] AS ca
            ON ca.CipherId = ucd.Id
           AND ca.UserId = @UserId
    WHERE
        ucd.Id = @Id
    GROUP BY
        ucd.Id,
        ucd.UserId,
        ucd.OrganizationId,
        ucd.Type,
        ucd.Data,
        ucd.Attachments,
        ucd.CreationDate,
        ucd.RevisionDate,
        ucd.Favorite,
        ucd.FolderId,
        ucd.DeletedDate,
        ucd.Reprompt,
        ucd.[Key],
        ucd.OrganizationUseTotp;
END
