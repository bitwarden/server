CREATE FUNCTION [dbo].[CipherDetails](@UserId UNIQUEIDENTIFIER)
RETURNS TABLE
AS RETURN
SELECT
    C.[Id],
    C.[UserId],
    C.[OrganizationId],
    C.[Type],
    C.[Data],
    C.[Attachments],
    C.[CreationDate],
    C.[RevisionDate],
    CASE
        WHEN
            @UserId IS NULL
            OR C.[Favorites] IS NULL
            OR JSON_VALUE(C.[Favorites], CONCAT('$."', @UserId, '"')) IS NULL
        THEN 0
        ELSE 1
    END [Favorite],
    CASE
        WHEN
            @UserId IS NULL
            OR C.[Folders] IS NULL
        THEN NULL
        ELSE TRY_CONVERT(UNIQUEIDENTIFIER, JSON_VALUE(C.[Folders], CONCAT('$."', @UserId, '"')))
    END [FolderId],
    C.[DeletedDate],
    C.[Reprompt]
FROM
    [dbo].[Cipher] C