CREATE VIEW [dbo].[CipherDetailsView]
AS
SELECT
    C.*,
    CASE WHEN F.[CipherId] IS NULL THEN 0 ELSE 1 END [Favorite],
    FC.[FolderId]
FROM
    [dbo].[Cipher] C
LEFT JOIN
    [dbo].[Favorite] F ON F.[CipherId] = C.[Id]
LEFT JOIN
    [dbo].[FolderCipher] FC ON FC.[CipherId] = C.[Id]