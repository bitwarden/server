-- Add new JSON column for Archives (similar to Favorites/Folders pattern)
IF NOT EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[dbo].[Cipher]')
    AND name = 'Archives'
)
BEGIN
    ALTER TABLE [dbo].[Cipher]
    ADD [Archives] NVARCHAR(MAX) NULL;
END;
GO

-- Update CipherDetails function to use JSON column approach
IF OBJECT_ID('[dbo].[CipherDetails]') IS NOT NULL
BEGIN
    DROP FUNCTION [dbo].[CipherDetails];
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
    C.[DeletedDate],
    C.[Reprompt],
    C.[Key],
    CASE
        WHEN
            @UserId IS NULL
            OR C.[Archives] IS NULL
        THEN NULL
        ELSE TRY_CONVERT(DATETIME2(7), JSON_VALUE(C.[Archives], CONCAT('$."', @UserId, '"')))
    END [ArchivedDate]
FROM
    [dbo].[Cipher] C;
GO

-- Update UserCipherDetails function
IF OBJECT_ID('[dbo].[UserCipherDetails]') IS NOT NULL
BEGIN
    DROP FUNCTION [dbo].[UserCipherDetails];
END
GO

CREATE FUNCTION [dbo].[UserCipherDetails](@UserId UNIQUEIDENTIFIER)
RETURNS TABLE
AS RETURN
WITH [CTE] AS (
    SELECT
        [Id],
        [OrganizationId]
    FROM
        [OrganizationUser]
    WHERE
        [UserId] = @UserId
        AND [Status] = 2 -- Confirmed
)
SELECT
    C.Id,
    C.UserId,
    C.OrganizationId,
    C.Type,
    C.Data,
    C.Attachments,
    C.CreationDate,
    C.RevisionDate,
    C.Favorite,
    C.FolderId,
    C.DeletedDate,
    C.ArchivedDate,
    C.Reprompt,
    C.[Key],
    CASE
        WHEN COALESCE(CU.[ReadOnly], CG.[ReadOnly], 0) = 0
        THEN 1
        ELSE 0
    END [Edit],
    CASE
        WHEN COALESCE(CU.[HidePasswords], CG.[HidePasswords], 0) = 0
        THEN 1
        ELSE 0
    END [ViewPassword],
    CASE
        WHEN COALESCE(CU.[Manage], CG.[Manage], 0) = 1
        THEN 1
        ELSE 0
    END [Manage],
    CASE
        WHEN O.[UseTotp] = 1
        THEN 1
        ELSE 0
    END [OrganizationUseTotp]
FROM
    [dbo].[CipherDetails](@UserId) C
INNER JOIN
    [CTE] OU ON C.[UserId] IS NULL AND C.[OrganizationId] IN (SELECT [OrganizationId] FROM [CTE])
INNER JOIN
    [dbo].[Organization] O ON O.[Id] = OU.[OrganizationId] AND O.[Id] = C.[OrganizationId] AND O.[Enabled] = 1
LEFT JOIN
    [dbo].[CollectionCipher] CC ON CC.[CipherId] = C.[Id]
LEFT JOIN
    [dbo].[CollectionUser] CU ON CU.[CollectionId] = CC.[CollectionId] AND CU.[OrganizationUserId] = OU.[Id]
LEFT JOIN
    [dbo].[GroupUser] GU ON CU.[CollectionId] IS NULL AND GU.[OrganizationUserId] = OU.[Id]
LEFT JOIN
    [dbo].[Group] G ON G.[Id] = GU.[GroupId]
LEFT JOIN
    [dbo].[CollectionGroup] CG ON CG.[CollectionId] = CC.[CollectionId] AND CG.[GroupId] = GU.[GroupId]
WHERE
    CU.[CollectionId] IS NOT NULL
    OR CG.[CollectionId] IS NOT NULL

UNION ALL

SELECT
    C.Id,
    C.UserId,
    C.OrganizationId,
    C.Type,
    C.Data,
    C.Attachments,
    C.CreationDate,
    C.RevisionDate,
    C.Favorite,
    C.FolderId,
    C.DeletedDate,
    C.ArchivedDate,
    C.Reprompt,
    C.[Key],
    1 [Edit],
    1 [ViewPassword],
    1 [Manage],
    0 [OrganizationUseTotp]
FROM
    [dbo].[CipherDetails](@UserId) AS C
WHERE
    C.[UserId] = @UserId;
GO

IF OBJECT_ID('[dbo].[Cipher_Archive]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Cipher_Archive];
END
GO

CREATE PROCEDURE [dbo].[Cipher_Archive]
    @Ids AS [dbo].[GuidIdArray] READONLY,
    @UserId AS UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    CREATE TABLE #Temp
    (
        [Id] UNIQUEIDENTIFIER NOT NULL,
        [UserId] UNIQUEIDENTIFIER NULL
    )

    INSERT INTO #Temp
    SELECT
        [Id],
        [UserId]
    FROM
        [dbo].[UserCipherDetails](@UserId)
    WHERE
        [Edit] = 1
      AND [ArchivedDate] IS NULL
      AND [Id] IN (SELECT * FROM @Ids)

    DECLARE @UtcNow DATETIME2(7) = SYSUTCDATETIME();
    UPDATE
        [dbo].[Cipher]
    SET
        [ArchivedDate] = JSON_MODIFY(
            COALESCE([Archives], N'{}'),
            '$."' + CONVERT(NVARCHAR(36), @UserId) + '"',
            @UtcNow
        ),
        [RevisionDate] = @UtcNow
    WHERE
        [Id] IN (SELECT [Id] FROM #Temp)

    EXEC [dbo].[User_BumpAccountRevisionDate] @UserId

    DROP TABLE #Temp

    SELECT @UtcNow
END
GO

IF OBJECT_ID('[dbo].[Cipher_Unarchive]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Cipher_Unarchive];
END
GO

CREATE PROCEDURE [dbo].[Cipher_Unarchive]
    @Ids AS [dbo].[GuidIdArray] READONLY,
    @UserId AS UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    CREATE TABLE #Temp
    (
        [Id] UNIQUEIDENTIFIER NOT NULL,
        [UserId] UNIQUEIDENTIFIER NULL
    )

    INSERT INTO #Temp
    SELECT
        [Id],
        [UserId]
    FROM
        [dbo].[UserCipherDetails](@UserId)
    WHERE
        [Edit] = 1
      AND [ArchivedDate] IS NOT NULL
      AND [Id] IN (SELECT * FROM @Ids)

    DECLARE @UtcNow DATETIME2(7) = SYSUTCDATETIME();
    UPDATE
        [dbo].[Cipher]
    SET
        [Archives] = JSON_MODIFY(
            COALESCE([Archives], N'{}'),
            '$."' + CONVERT(NVARCHAR(36), @UserId) + '"',
            NULL
        ),
        [RevisionDate] = @UtcNow
    WHERE
        [Id] IN (SELECT [Id] FROM #Temp)

    EXEC [dbo].[User_BumpAccountRevisionDate] @UserId

    DROP TABLE #Temp

    SELECT @UtcNow
END
GO
