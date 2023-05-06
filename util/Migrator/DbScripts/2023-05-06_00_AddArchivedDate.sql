-- Add ArchivedDate column to Cipher
IF COL_LENGTH('[dbo].[Cipher]', 'ArchivedDate') IS NULL
BEGIN
    ALTER TABLE [dbo].[Cipher]
        ADD [ArchivedDate] DATETIME2(7) NULL;
END
GO

IF OBJECT_ID('[dbo].[CipherDetails]') IS NOT NULL
BEGIN
    DROP FUNCTION [dbo].[CipherDetails]
END
GO

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
    C.[ArchivedDate],
    C.[DeletedDate],
    C.[Reprompt]
FROM
    [dbo].[Cipher] C
GO

IF OBJECT_ID('[dbo].[UserCipherDetails]') IS NOT NULL
BEGIN
    EXECUTE sp_refreshsqlmodule N'[dbo].[UserCipherDetails]';
END
GO

IF OBJECT_ID('[dbo].[CipherView]') IS NOT NULL
BEGIN
    EXECUTE sp_refreshsqlmodule N'[dbo].[CipherView]';
END
GO