/**
 * Revert [Cipher] deletes/gets to original versions
 * - No longer needs to have the deleted flag on reads (always read all)
 * - No longer needs to have the permanent flag on deletes (they just are)
 * + Added ability to bulk soft-delete a cipher
 * + Added ability to bulk restore a soft-deleted cipher
 * + Added DeletedDate value to updates/create sprocs (individual records)
 */
IF OBJECT_ID('[dbo].[Cipher_Restore]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Cipher_Restore];
END
GO
CREATE PROCEDURE [dbo].[Cipher_Restore]
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
        [dbo].[UserCipherDetails](@UserId)
    WHERE
        [Edit] = 1
        AND [Id] IN (SELECT * FROM @Ids)
	
    UPDATE
        [dbo].[Cipher]
    SET
        [DeletedDate] = NULL,
        [RevisionDate] = GETUTCDATE()
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
END
GO

IF OBJECT_ID('[dbo].[Cipher_RestoreById]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Cipher_RestoreById];
END
GO

IF OBJECT_ID('[dbo].[CipherDetails_ReadByUserId]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[CipherDetails_ReadByUserId];
END
GO
CREATE PROCEDURE [dbo].[CipherDetails_ReadByUserId]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[UserCipherDetails](@UserId)
END
GO

IF OBJECT_ID('[dbo].[CipherDetails_ReadWithoutOrganizationsByUserId]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[CipherDetails_ReadWithoutOrganizationsByUserId];
END
GO
CREATE PROCEDURE [dbo].[CipherDetails_ReadWithoutOrganizationsByUserId]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *,
        1 [Edit],
        0 [OrganizationUseTotp]
    FROM
        [dbo].[CipherDetails](@UserId)
    WHERE
        [UserId] = @UserId
END
GO

IF OBJECT_ID('[dbo].[CipherOrganizationDetails_ReadById]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[CipherOrganizationDetails_ReadById];
END
GO
CREATE PROCEDURE [dbo].[CipherOrganizationDetails_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        C.*,
        CASE 
            WHEN O.[UseTotp] = 1 THEN 1
            ELSE 0
        END [OrganizationUseTotp]
    FROM
        [dbo].[CipherView] C
    LEFT JOIN
        [dbo].[Organization] O ON O.[Id] = C.[OrganizationId]
    WHERE
        C.[Id] = @Id
END
GO

IF OBJECT_ID('[dbo].[Cipher_Delete]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Cipher_Delete];
END
GO
CREATE PROCEDURE [dbo].[Cipher_Delete]
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
        [dbo].[UserCipherDetails](@UserId)
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

IF OBJECT_ID('[dbo].[Cipher_DeleteById]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Cipher_DeleteById];
END
GO
CREATE PROCEDURE [dbo].[Cipher_DeleteById]
    @Id UNIQUEIDENTIFIER
WITH RECOMPILE
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @UserId UNIQUEIDENTIFIER
    DECLARE @OrganizationId UNIQUEIDENTIFIER
    DECLARE @Attachments BIT

    SELECT TOP 1
        @UserId = [UserId],
        @OrganizationId = [OrganizationId],
        @Attachments = CASE WHEN [Attachments] IS NOT NULL THEN 1 ELSE 0 END
    FROM
        [dbo].[Cipher]
    WHERE
        [Id] = @Id

    DELETE
    FROM
        [dbo].[Cipher]
    WHERE
        [Id] = @Id

    IF @OrganizationId IS NOT NULL
    BEGIN
        IF @Attachments = 1
        BEGIN
            EXEC [dbo].[Organization_UpdateStorage] @OrganizationId
        END
        EXEC [dbo].[User_BumpAccountRevisionDateByCipherId] @Id, @OrganizationId
    END
    ELSE IF @UserId IS NOT NULL
    BEGIN
        IF @Attachments = 1
        BEGIN
            EXEC [dbo].[User_UpdateStorage] @UserId
        END
        EXEC [dbo].[User_BumpAccountRevisionDate] @UserId
    END
END
GO

IF OBJECT_ID('[dbo].[Cipher_ReadByOrganizationId]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Cipher_ReadByOrganizationId];
END
GO
CREATE PROCEDURE [dbo].[Cipher_ReadByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[CipherView]
    WHERE
        [UserId] IS NULL
        AND [OrganizationId] = @OrganizationId
END
GO

IF OBJECT_ID('[dbo].[Cipher_SoftDelete]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Cipher_SoftDelete];
END
GO
CREATE PROCEDURE [dbo].[Cipher_SoftDelete]
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
        [dbo].[UserCipherDetails](@UserId)
    WHERE
        [Edit] = 1
        AND [Id] IN (SELECT * FROM @Ids)

    -- Delete ciphers
    UPDATE
        [dbo].[Cipher]
    SET
        [DeletedDate] = SYSUTCDATETIME(),
        [RevisionDate] = GETUTCDATE()
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

IF OBJECT_ID('[dbo].[Cipher_SoftDeleteById]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Cipher_SoftDeleteById];
END
GO

IF OBJECT_ID('[dbo].[Cipher_Create]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Cipher_Create];
END
GO
CREATE PROCEDURE [dbo].[Cipher_Create]
    @Id UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @Type TINYINT,
    @Data NVARCHAR(MAX),
    @Favorites NVARCHAR(MAX),
    @Folders NVARCHAR(MAX),
    @Attachments NVARCHAR(MAX),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @DeletedDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[Cipher]
    (
        [Id],
        [UserId],
        [OrganizationId],
        [Type],
        [Data],
        [Favorites],
        [Folders],
        [Attachments],
        [CreationDate],
        [RevisionDate],
        [DeletedDate]
    )
    VALUES
    (
        @Id,
        CASE WHEN @OrganizationId IS NULL THEN @UserId ELSE NULL END,
        @OrganizationId,
        @Type,
        @Data,
        @Favorites,
        @Folders,
        @Attachments,
        @CreationDate,
        @RevisionDate,
        @DeletedDate
    )

    IF @OrganizationId IS NOT NULL
    BEGIN
        EXEC [dbo].[User_BumpAccountRevisionDateByCipherId] @Id, @OrganizationId
    END
    ELSE IF @UserId IS NOT NULL
    BEGIN
        EXEC [dbo].[User_BumpAccountRevisionDate] @UserId
    END
END
GO

IF OBJECT_ID('[dbo].[CipherDetails_Create]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[CipherDetails_Create];
END
GO
CREATE PROCEDURE [dbo].[CipherDetails_Create]
    @Id UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @Type TINYINT,
    @Data NVARCHAR(MAX),
    @Favorites NVARCHAR(MAX), -- not used
    @Folders NVARCHAR(MAX), -- not used
    @Attachments NVARCHAR(MAX), -- not used
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @FolderId UNIQUEIDENTIFIER,
    @Favorite BIT,
    @Edit BIT, -- not used
    @OrganizationUseTotp BIT, -- not used
    @DeletedDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @UserIdKey VARCHAR(50) = CONCAT('"', @UserId, '"')
    DECLARE @UserIdPath VARCHAR(50) = CONCAT('$.', @UserIdKey)

    INSERT INTO [dbo].[Cipher]
    (
        [Id],
        [UserId],
        [OrganizationId],
        [Type],
        [Data],
        [Favorites],
        [Folders],
        [CreationDate],
        [RevisionDate],
        [DeletedDate]
    )
    VALUES
    (
        @Id,
        CASE WHEN @OrganizationId IS NULL THEN @UserId ELSE NULL END,
        @OrganizationId,
        @Type,
        @Data,
        CASE WHEN @Favorite = 1 THEN CONCAT('{', @UserIdKey, ':true}') ELSE NULL END,
        CASE WHEN @FolderId IS NOT NULL THEN CONCAT('{', @UserIdKey, ':"', @FolderId, '"', '}') ELSE NULL END,
        @CreationDate,
        @RevisionDate,
        @DeletedDate
    )

    IF @OrganizationId IS NOT NULL
    BEGIN
        EXEC [dbo].[User_BumpAccountRevisionDateByCipherId] @Id, @OrganizationId
    END
    ELSE IF @UserId IS NOT NULL
    BEGIN
        EXEC [dbo].[User_BumpAccountRevisionDate] @UserId
    END
END
GO

IF OBJECT_ID('[dbo].[Cipher_CreateWithCollections]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Cipher_CreateWithCollections];
END
GO
CREATE PROCEDURE [dbo].[Cipher_CreateWithCollections]
    @Id UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @Type TINYINT,
    @Data NVARCHAR(MAX),
    @Favorites NVARCHAR(MAX),
    @Folders NVARCHAR(MAX),
    @Attachments NVARCHAR(MAX),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @DeletedDate DATETIME2(7),
    @CollectionIds AS [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    EXEC [dbo].[Cipher_Create] @Id, @UserId, @OrganizationId, @Type, @Data, @Favorites, @Folders,
        @Attachments, @CreationDate, @RevisionDate, @DeletedDate

    DECLARE @UpdateCollectionsSuccess INT
    EXEC @UpdateCollectionsSuccess = [dbo].[Cipher_UpdateCollections] @Id, @UserId, @OrganizationId, @CollectionIds
END
GO

IF OBJECT_ID('[dbo].[CipherDetails_CreateWithCollections]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[CipherDetails_CreateWithCollections];
END
GO
CREATE PROCEDURE [dbo].[CipherDetails_CreateWithCollections]
    @Id UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @Type TINYINT,
    @Data NVARCHAR(MAX),
    @Favorites NVARCHAR(MAX), -- not used
    @Folders NVARCHAR(MAX), -- not used
    @Attachments NVARCHAR(MAX), -- not used
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @FolderId UNIQUEIDENTIFIER,
    @Favorite BIT,
    @Edit BIT, -- not used
    @OrganizationUseTotp BIT, -- not used
    @DeletedDate DATETIME2(7),
    @CollectionIds AS [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    EXEC [dbo].[CipherDetails_Create] @Id, @UserId, @OrganizationId, @Type, @Data, @Favorites, @Folders,
        @Attachments, @CreationDate, @RevisionDate, @FolderId, @Favorite, @Edit, @OrganizationUseTotp, @DeletedDate

    DECLARE @UpdateCollectionsSuccess INT
    EXEC @UpdateCollectionsSuccess = [dbo].[Cipher_UpdateCollections] @Id, @UserId, @OrganizationId, @CollectionIds
END
GO

IF OBJECT_ID('[dbo].[Cipher_Update]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Cipher_Update];
END
GO
CREATE PROCEDURE [dbo].[Cipher_Update]
    @Id UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @Type TINYINT,
    @Data NVARCHAR(MAX),
    @Favorites NVARCHAR(MAX),
    @Folders NVARCHAR(MAX),
    @Attachments NVARCHAR(MAX),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @DeletedDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[Cipher]
    SET
        [UserId] = CASE WHEN @OrganizationId IS NULL THEN @UserId ELSE NULL END,
        [OrganizationId] = @OrganizationId,
        [Type] = @Type,
        [Data] = @Data,
        [Favorites] = @Favorites,
        [Folders] = @Folders,
        [Attachments] = @Attachments,
        [CreationDate] = @CreationDate,
        [RevisionDate] = @RevisionDate,
        [DeletedDate] = @DeletedDate
    WHERE
        [Id] = @Id

    IF @OrganizationId IS NOT NULL
    BEGIN
        EXEC [dbo].[User_BumpAccountRevisionDateByCipherId] @Id, @OrganizationId
    END
    ELSE IF @UserId IS NOT NULL
    BEGIN
        EXEC [dbo].[User_BumpAccountRevisionDate] @UserId
    END
END
GO

IF OBJECT_ID('[dbo].[Cipher_UpdateWithCollections]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Cipher_UpdateWithCollections];
END
GO
CREATE PROCEDURE [dbo].[Cipher_UpdateWithCollections]
    @Id UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @Type TINYINT,
    @Data NVARCHAR(MAX),
    @Favorites NVARCHAR(MAX),
    @Folders NVARCHAR(MAX),
    @Attachments NVARCHAR(MAX),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @DeletedDate DATETIME2(7),
    @CollectionIds AS [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    BEGIN TRANSACTION Cipher_UpdateWithCollections

    DECLARE @UpdateCollectionsSuccess INT
    EXEC @UpdateCollectionsSuccess = [dbo].[Cipher_UpdateCollections] @Id, @UserId, @OrganizationId, @CollectionIds

    IF @UpdateCollectionsSuccess < 0
    BEGIN
        COMMIT TRANSACTION Cipher_UpdateWithCollections
        SELECT -1 -- -1 = Failure
        RETURN
    END

    UPDATE
        [dbo].[Cipher]
    SET
        [UserId] = NULL,
        [OrganizationId] = @OrganizationId,
        [Data] = @Data,
        [Attachments] = @Attachments,
        [RevisionDate] = @RevisionDate,
        [DeletedDate] = @DeletedDate
        -- No need to update CreationDate, Favorites, Folders, or Type since that data will not change
    WHERE
        [Id] = @Id

    COMMIT TRANSACTION Cipher_UpdateWithCollections

    IF @Attachments IS NOT NULL
    BEGIN
        EXEC [dbo].[Organization_UpdateStorage] @OrganizationId
        EXEC [dbo].[User_UpdateStorage] @UserId
    END

    EXEC [dbo].[User_BumpAccountRevisionDateByCipherId] @Id, @OrganizationId

    SELECT 0 -- 0 = Success
END
GO

IF OBJECT_ID('[dbo].[CipherDetails_Update]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[CipherDetails_Update];
END
GO
CREATE PROCEDURE [dbo].[CipherDetails_Update]
    @Id UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @Type TINYINT,
    @Data NVARCHAR(MAX),
    @Favorites NVARCHAR(MAX), -- not used
    @Folders NVARCHAR(MAX), -- not used
    @Attachments NVARCHAR(MAX), -- not used
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @FolderId UNIQUEIDENTIFIER,
    @Favorite BIT,
    @Edit BIT, -- not used
    @OrganizationUseTotp BIT, -- not used
    @DeletedDate DATETIME2(2)
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @UserIdKey VARCHAR(50) = CONCAT('"', @UserId, '"')
    DECLARE @UserIdPath VARCHAR(50) = CONCAT('$.', @UserIdKey)

    UPDATE
        [dbo].[Cipher]
    SET
        [UserId] = CASE WHEN @OrganizationId IS NULL THEN @UserId ELSE NULL END,
        [OrganizationId] = @OrganizationId,
        [Type] = @Type,
        [Data] = @Data,
        [Folders] = 
            CASE
            WHEN @FolderId IS NOT NULL AND [Folders] IS NULL THEN
                CONCAT('{', @UserIdKey, ':"', @FolderId, '"', '}')
            WHEN @FolderId IS NOT NULL THEN
                JSON_MODIFY([Folders], @UserIdPath, CAST(@FolderId AS VARCHAR(50)))
            ELSE
                JSON_MODIFY([Folders], @UserIdPath, NULL)
            END,
        [Favorites] =
            CASE
            WHEN @Favorite = 1 AND [Favorites] IS NULL THEN
                CONCAT('{', @UserIdKey, ':true}')
            WHEN @Favorite = 1 THEN
                JSON_MODIFY([Favorites], @UserIdPath, CAST(1 AS BIT))
            ELSE
                JSON_MODIFY([Favorites], @UserIdPath, NULL)
            END,
        [CreationDate] = @CreationDate,
        [RevisionDate] = @RevisionDate,
        [DeletedDate] = @DeletedDate
    WHERE
        [Id] = @Id

    IF @OrganizationId IS NOT NULL
    BEGIN
        EXEC [dbo].[User_BumpAccountRevisionDateByCipherId] @Id, @OrganizationId
    END
    ELSE IF @UserId IS NOT NULL
    BEGIN
        EXEC [dbo].[User_BumpAccountRevisionDate] @UserId
    END
END
GO

/*
IF OBJECT_ID('[dbo].') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].;
END
GO

GO
*/