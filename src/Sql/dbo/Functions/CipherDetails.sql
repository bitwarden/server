CREATE FUNCTION [dbo].[CipherDetails](@UserId UNIQUEIDENTIFIER)
RETURNS TABLE
AS RETURN
SELECT
    C.*,
    CASE WHEN F.[CipherId] IS NULL THEN 0 ELSE 1 END [Favorite],
    FO.[Id] [FolderId]
FROM
    [dbo].[Cipher] C
LEFT JOIN
    [dbo].[Favorite] F ON F.[CipherId] = C.[Id] AND F.[UserId] = @UserId
LEFT JOIN
    [dbo].[FolderCipher] FC ON FC.[CipherId] = C.[Id]
LEFT JOIN
    [dbo].[Folder] FO ON FO.[Id] = FC.[FolderId] AND FO.[UserId] = @UserId