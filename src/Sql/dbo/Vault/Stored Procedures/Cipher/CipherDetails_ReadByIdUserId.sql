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
        -- The highest rank holds Manage as 4, Edit as 2 and ViewPassword as 1; each flag is one part of it
        (MAX(R.[Rank]) / 2) % 2 AS [Edit],
        MAX(R.[Rank]) % 2 AS [ViewPassword],
        MAX(R.[Rank]) / 4 AS [Manage]
    FROM
        [dbo].[UserCipherDetails](@UserId) UCD
    -- Rank orders a grant by Manage, then Edit, then ViewPassword, so the highest permission level wins
    CROSS APPLY (
        SELECT UCD.[Manage] * 4 + UCD.[Edit] * 2 + UCD.[ViewPassword] AS [Rank]
    ) R
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
