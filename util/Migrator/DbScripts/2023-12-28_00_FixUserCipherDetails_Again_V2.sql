-- 2023-11-28_00_DeprecateAccessAll_UserCipherDetails introduced a bug in UserCipherDetails_V2 which inverted
-- the ReadOnly and HidePasswords logic. That should have been fixed in 2023-11-29_00_FixUserCipherDetails_V2
-- but for some reason that sproc was not being run in cloud environments, and/or may not have refreshed
-- metadata of sprocs that used that function.

-- This migration just replicates the original 2023-11-28 migration but with the fix from 2023-11-29.

CREATE OR ALTER FUNCTION [dbo].[UserCipherDetails_V2](@UserId UNIQUEIDENTIFIER)
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
    C.*,
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
    *,
    1 [Edit],
    1 [ViewPassword],
    0 [OrganizationUseTotp]
FROM
    [dbo].[CipherDetails](@UserId)
WHERE
    [UserId] = @UserId
GO

-- Create v2 sprocs for all sprocs that call UserCipherDetails

-- CipherDetails_ReadByIdUserId_V2
CREATE OR ALTER PROCEDURE [dbo].[CipherDetails_ReadByIdUserId_V2]
    @Id UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT TOP 1
        *
    FROM
        [dbo].[UserCipherDetails_V2](@UserId)
    WHERE
        [Id] = @Id
    ORDER BY
        [Edit] DESC
END
GO

-- CipherDetails_ReadByUserId_V2
CREATE OR ALTER PROCEDURE [dbo].[CipherDetails_ReadByUserId_V2]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[UserCipherDetails_V2](@UserId)
END
GO

-- Cipher_Delete_V2
CREATE OR ALTER PROCEDURE [dbo].[Cipher_Delete_V2]
    @Ids AS [dbo].[GuidIdArray] READONLY,
    @UserId AS UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    CREATE TABLE #Temp
    (
        [Id] UNIQUEIDENTIFIER NOT NULL,
        [UserId] UNIQUEIDENTIFIER NULL,
        [OrganizationId] UNIQUEIDENTIFIER NULL,
        [Attachments] BIT NOT NULL
    )

    INSERT INTO #Temp
    SELECT
        [Id],
        [UserId],
        [OrganizationId],
        CASE WHEN [Attachments] IS NULL THEN 0 ELSE 1 END
    FROM
        [dbo].[UserCipherDetails_V2](@UserId)
    WHERE
        [Edit] = 1
        AND [Id] IN (SELECT * FROM @Ids)

    -- Delete ciphers
    DELETE
    FROM
        [dbo].[Cipher]
    WHERE
        [Id] IN (SELECT [Id] FROM #Temp)

    -- Cleanup orgs
    DECLARE @OrgId UNIQUEIDENTIFIER
    DECLARE [OrgCursor] CURSOR FORWARD_ONLY FOR
        SELECT
            [OrganizationId]
        FROM
            #Temp
        WHERE
            [OrganizationId] IS NOT NULL
        GROUP BY
            [OrganizationId]
    OPEN [OrgCursor]
    FETCH NEXT FROM [OrgCursor] INTO @OrgId
    WHILE @@FETCH_STATUS = 0 BEGIN
        EXEC [dbo].[Organization_UpdateStorage] @OrgId
        EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationId] @OrgId
        FETCH NEXT FROM [OrgCursor] INTO @OrgId
    END
    CLOSE [OrgCursor]
    DEALLOCATE [OrgCursor]

    -- Cleanup user
    DECLARE @UserCiphersWithStorageCount INT
    SELECT
        @UserCiphersWithStorageCount = COUNT(1)
    FROM
        #Temp
    WHERE
        [UserId] IS NOT NULL
        AND [Attachments] = 1

    IF @UserCiphersWithStorageCount > 0
    BEGIN
        EXEC [dbo].[User_UpdateStorage] @UserId
    END
    EXEC [dbo].[User_BumpAccountRevisionDate] @UserId

    DROP TABLE #Temp
END
GO

-- Cipher_Move_V2
CREATE OR ALTER PROCEDURE [dbo].[Cipher_Move_V2]
    @Ids AS [dbo].[GuidIdArray] READONLY,
    @FolderId AS UNIQUEIDENTIFIER,
    @UserId AS UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @UserIdKey VARCHAR(50) = CONCAT('"', @UserId, '"')
    DECLARE @UserIdPath VARCHAR(50) = CONCAT('$.', @UserIdKey)

    ;WITH [IdsToMoveCTE] AS (
        SELECT
            [Id]
        FROM
            [dbo].[UserCipherDetails_V2](@UserId)
        WHERE
            [Id] IN (SELECT * FROM @Ids)
    )
    UPDATE
        [dbo].[Cipher]
    SET
        [Folders] =
            CASE
            WHEN @FolderId IS NOT NULL AND [Folders] IS NULL THEN
                CONCAT('{', @UserIdKey, ':"', @FolderId, '"', '}')
            WHEN @FolderId IS NOT NULL THEN
                JSON_MODIFY([Folders], @UserIdPath, CAST(@FolderId AS VARCHAR(50)))
            ELSE
                JSON_MODIFY([Folders], @UserIdPath, NULL)
            END
    WHERE
        [Id] IN (SELECT * FROM [IdsToMoveCTE])

    EXEC [dbo].[User_BumpAccountRevisionDate] @UserId
END
GO

-- Cipher_Restore_V2
CREATE OR ALTER PROCEDURE [dbo].[Cipher_Restore_V2]
    @Ids AS [dbo].[GuidIdArray] READONLY,
    @UserId AS UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    CREATE TABLE #Temp
    (
        [Id] UNIQUEIDENTIFIER NOT NULL,
        [UserId] UNIQUEIDENTIFIER NULL,
        [OrganizationId] UNIQUEIDENTIFIER NULL
    )

    INSERT INTO #Temp
    SELECT
        [Id],
        [UserId],
        [OrganizationId]
    FROM
        [dbo].[UserCipherDetails_V2](@UserId)
    WHERE
        [Edit] = 1
        AND [DeletedDate] IS NOT NULL
        AND [Id] IN (SELECT * FROM @Ids)

    DECLARE @UtcNow DATETIME2(7) = GETUTCDATE();
    UPDATE
        [dbo].[Cipher]
    SET
        [DeletedDate] = NULL,
        [RevisionDate] = @UtcNow
    WHERE
        [Id] IN (SELECT [Id] FROM #Temp)

    -- Bump orgs
    DECLARE @OrgId UNIQUEIDENTIFIER
    DECLARE [OrgCursor] CURSOR FORWARD_ONLY FOR
        SELECT
            [OrganizationId]
        FROM
            #Temp
        WHERE
            [OrganizationId] IS NOT NULL
        GROUP BY
            [OrganizationId]
    OPEN [OrgCursor]
    FETCH NEXT FROM [OrgCursor] INTO @OrgId
    WHILE @@FETCH_STATUS = 0 BEGIN
        EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationId] @OrgId
        FETCH NEXT FROM [OrgCursor] INTO @OrgId
    END
    CLOSE [OrgCursor]
    DEALLOCATE [OrgCursor]

    -- Bump user
    EXEC [dbo].[User_BumpAccountRevisionDate] @UserId

    DROP TABLE #Temp

    SELECT @UtcNow
END
GO

-- Cipher_SoftDelete_V2
CREATE OR ALTER PROCEDURE [dbo].[Cipher_SoftDelete_V2]
    @Ids AS [dbo].[GuidIdArray] READONLY,
    @UserId AS UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    CREATE TABLE #Temp
    (
        [Id] UNIQUEIDENTIFIER NOT NULL,
        [UserId] UNIQUEIDENTIFIER NULL,
        [OrganizationId] UNIQUEIDENTIFIER NULL
    )

    INSERT INTO #Temp
    SELECT
        [Id],
        [UserId],
        [OrganizationId]
    FROM
        [dbo].[UserCipherDetails_V2](@UserId)
    WHERE
        [Edit] = 1
        AND [DeletedDate] IS NULL
        AND [Id] IN (SELECT * FROM @Ids)

    -- Delete ciphers
    DECLARE @UtcNow DATETIME2(7) = GETUTCDATE();
    UPDATE
        [dbo].[Cipher]
    SET
        [DeletedDate] = @UtcNow,
        [RevisionDate] = @UtcNow
    WHERE
        [Id] IN (SELECT [Id] FROM #Temp)

    -- Cleanup orgs
    DECLARE @OrgId UNIQUEIDENTIFIER
    DECLARE [OrgCursor] CURSOR FORWARD_ONLY FOR
        SELECT
            [OrganizationId]
        FROM
            #Temp
        WHERE
            [OrganizationId] IS NOT NULL
        GROUP BY
            [OrganizationId]
    OPEN [OrgCursor]
    FETCH NEXT FROM [OrgCursor] INTO @OrgId
    WHILE @@FETCH_STATUS = 0 BEGIN
        EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationId] @OrgId
        FETCH NEXT FROM [OrgCursor] INTO @OrgId
    END
    CLOSE [OrgCursor]
    DEALLOCATE [OrgCursor]

    EXEC [dbo].[User_BumpAccountRevisionDate] @UserId

    DROP TABLE #Temp
END
GO
