CREATE VIEW [dbo].[CipherShareView]
AS
SELECT
    C.[Id],
    C.[UserId],
    C.[FolderId],
    C.[Type],
    C.[Favorite],
    ISNULL(S.[Key], C.[Key]) [Key],
    C.[Data],
    C.[CreationDate],
    C.[RevisionDate],
    S.[ReadOnly],
    S.[Status],
    S.[UserId] [ShareUserId]
FROM
    [dbo].[Cipher] C
LEFT JOIN
    [dbo].[Share] S ON C.[Id] = S.[CipherId]