CREATE PROCEDURE [dbo].[CipherDetails_ReadByIdUserId]
    @Id UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

SELECT
        [Id],
        [UserId],
        [OrganizationId],
        [Type],
        [Data],
        [Attachments],
        [CreationDate],
        [RevisionDate],
        [Favorite],
        [FolderId],
        [DeletedDate],
        [Reprompt],
        [Key],
        [OrganizationUseTotp],
        [ArchivedDate],
        MAX ([Edit]) AS [Edit],
        MAX ([ViewPassword]) AS [ViewPassword],
        MAX ([Manage]) AS [Manage]
    FROM
        [dbo].[UserCipherDetails](@UserId)
    WHERE
        [Id] = @Id
    GROUP BY
        [Id],
        [UserId],
        [OrganizationId],
        [Type],
        [Data],
        [Attachments],
        [CreationDate],
        [RevisionDate],
        [Favorite],
        [FolderId],
        [DeletedDate],
        [Reprompt],
        [Key],
        [OrganizationUseTotp],
        [ArchivedDate]
END
