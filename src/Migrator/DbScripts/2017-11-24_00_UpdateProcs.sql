IF OBJECT_ID('[dbo].[User_BumpAccountRevisionDateByCipherId]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[User_BumpAccountRevisionDateByCipherId]
END
GO

CREATE PROCEDURE [dbo].[User_BumpAccountRevisionDateByCipherId]
    @CipherId UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        U
    SET
        U.[AccountRevisionDate] = GETUTCDATE()
    FROM
        [dbo].[User] U
    LEFT JOIN
        [dbo].[OrganizationUser] OU ON OU.[UserId] = U.[Id]
    LEFT JOIN
        [dbo].[CollectionCipher] CC ON CC.[CipherId] = @CipherId
    LEFT JOIN
        [dbo].[CollectionUser] CU ON OU.[AccessAll] = 0 AND CU.[OrganizationUserId] = OU.[Id] AND CU.[CollectionId] = CC.[CollectionId]
    LEFT JOIN
        [dbo].[GroupUser] GU ON CU.[CollectionId] IS NULL AND OU.[AccessAll] = 0 AND GU.[OrganizationUserId] = OU.[Id]
    LEFT JOIN
        [dbo].[Group] G ON G.[Id] = GU.[GroupId]
    LEFT JOIN
        [dbo].[CollectionGroup] CG ON G.[AccessAll] = 0 AND CG.[GroupId] = GU.[GroupId] AND CG.[CollectionId] = CC.[CollectionId]
    WHERE
        OU.[Status] = 2 -- 2 = Confirmed
        AND (
            CU.[CollectionId] IS NOT NULL
            OR CG.[CollectionId] IS NOT NULL
            OR (
                OU.[OrganizationId] = @OrganizationId
                AND (
                    OU.[AccessAll] = 1
                    OR G.[AccessAll] = 1
                )
            )
        )
END
GO

IF OBJECT_ID('[dbo].[User_BumpAccountRevisionDateByCollectionId]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[User_BumpAccountRevisionDateByCollectionId]
END
GO

CREATE PROCEDURE [dbo].[User_BumpAccountRevisionDateByCollectionId]
    @CollectionId UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        U
    SET
        U.[AccountRevisionDate] = GETUTCDATE()
    FROM
        [dbo].[User] U
    LEFT JOIN
        [dbo].[OrganizationUser] OU ON OU.[UserId] = U.[Id]
    LEFT JOIN
        [dbo].[CollectionUser] CU ON OU.[AccessAll] = 0 AND CU.[OrganizationUserId] = OU.[Id] AND CU.[CollectionId] = @CollectionId
    LEFT JOIN
        [dbo].[GroupUser] GU ON CU.[CollectionId] IS NULL AND OU.[AccessAll] = 0 AND GU.[OrganizationUserId] = OU.[Id]
    LEFT JOIN
        [dbo].[Group] G ON G.[Id] = GU.[GroupId]
    LEFT JOIN
        [dbo].[CollectionGroup] CG ON G.[AccessAll] = 0 AND CG.[GroupId] = GU.[GroupId] AND CG.[CollectionId] = @CollectionId
    WHERE
        OU.[Status] = 2 -- 2 = Confirmed
        AND (
            CU.[CollectionId] IS NOT NULL
            OR CG.[CollectionId] IS NOT NULL
            OR (
                OU.[OrganizationId] = @OrganizationId
                AND (
                    OU.[AccessAll] = 1
                    OR G.[AccessAll] = 1
                )
            )
        )
END
GO

IF OBJECT_ID('[dbo].[Cipher_DeleteByUserId]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Cipher_DeleteByUserId]
END
GO

CREATE PROCEDURE [dbo].[Cipher_DeleteByUserId]
    @UserId AS UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @BatchSize INT = 100

    -- Delete ciphers
    WHILE @BatchSize > 0
    BEGIN
        BEGIN TRANSACTION Cipher_DeleteByUserId_Ciphers

        DELETE TOP(@BatchSize)
        FROM
            [dbo].[Cipher]
        WHERE
            [UserId] = @UserId

        SET @BatchSize = @@ROWCOUNT

        COMMIT TRANSACTION Cipher_DeleteByUserId_Ciphers
    END

    -- Delete folders
    DELETE
    FROM
        [dbo].[Folder]
    WHERE
        [UserId] = @UserId

    -- Cleanup user
    EXEC [dbo].[User_UpdateStorage] @UserId
    EXEC [dbo].[User_BumpAccountRevisionDate] @UserId
END
GO

IF OBJECT_ID('[dbo].[User_DeleteById]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[User_DeleteById]
END
GO

CREATE PROCEDURE [dbo].[User_DeleteById]
    @Id UNIQUEIDENTIFIER
WITH RECOMPILE
AS
BEGIN
    SET NOCOUNT ON
    DECLARE @BatchSize INT = 100

    -- Delete ciphers
    WHILE @BatchSize > 0
    BEGIN
        BEGIN TRANSACTION User_DeleteById_Ciphers

        DELETE TOP(@BatchSize)
        FROM
            [dbo].[Cipher]
        WHERE
            [UserId] = @Id

        SET @BatchSize = @@ROWCOUNT

        COMMIT TRANSACTION User_DeleteById_Ciphers
    END

    BEGIN TRANSACTION User_DeleteById

    -- Delete folders
    DELETE
    FROM
        [dbo].[Folder]
    WHERE
        [UserId] = @Id

    -- Delete devices
    DELETE
    FROM
        [dbo].[Device]
    WHERE
        [UserId] = @Id

    -- Delete collection users
    DELETE
        CU
    FROM
        [dbo].[CollectionUser] CU
    INNER JOIN
        [dbo].[OrganizationUser] OU ON OU.[Id] = CU.[OrganizationUserId]
    WHERE
        OU.[UserId] = @Id

    -- Delete group users
    DELETE
        GU
    FROM
        [dbo].[GroupUser] GU
    INNER JOIN
        [dbo].[OrganizationUser] OU ON OU.[Id] = GU.[OrganizationUserId]
    WHERE
        OU.[UserId] = @Id

    -- Delete organization users
    DELETE
    FROM
        [dbo].[OrganizationUser]
    WHERE
        [UserId] = @Id

    -- Delete U2F logins
    DELETE
    FROM
        [dbo].[U2f]
    WHERE
        [UserId] = @Id

    -- Finally, delete the user
    DELETE
    FROM
        [dbo].[User]
    WHERE
        [Id] = @Id

    COMMIT TRANSACTION User_DeleteById
END
GO

IF OBJECT_ID('[dbo].[CipherDetails_Create]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[CipherDetails_Create]
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
    @OrganizationUseTotp BIT -- not used
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
        [RevisionDate]
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
        @RevisionDate
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

IF OBJECT_ID('[dbo].[CipherDetails_Update]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[CipherDetails_Update]
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
    @OrganizationUseTotp BIT -- not used
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
        [RevisionDate] = @RevisionDate
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

IF OBJECT_ID('[dbo].[Cipher_Create]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Cipher_Create]
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
    @RevisionDate DATETIME2(7)
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
        [RevisionDate]
    )
    VALUES
    (
        @Id,
        @UserId,
        @OrganizationId,
        @Type,
        @Data,
        @Favorites,
        @Folders,
        @Attachments,
        @CreationDate,
        @RevisionDate
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

IF OBJECT_ID('[dbo].[Cipher_DeleteAttachment]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Cipher_DeleteAttachment]
END
GO

CREATE PROCEDURE [dbo].[Cipher_DeleteAttachment]
    @Id UNIQUEIDENTIFIER,
    @AttachmentId VARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @AttachmentIdKey VARCHAR(50) = CONCAT('"', @AttachmentId, '"')
    DECLARE @AttachmentIdPath VARCHAR(50) = CONCAT('$.', @AttachmentIdKey)

    DECLARE @UserId UNIQUEIDENTIFIER
    DECLARE @OrganizationId UNIQUEIDENTIFIER

    SELECT
        @UserId = [UserId],
        @OrganizationId = [OrganizationId]
    FROM 
        [dbo].[Cipher]
    WHERE [Id] = @Id

    UPDATE
        [dbo].[Cipher]
    SET
        [Attachments] = JSON_MODIFY([Attachments], @AttachmentIdPath, NULL)
    WHERE
        [Id] = @Id

    IF @OrganizationId IS NOT NULL
    BEGIN
        EXEC [dbo].[Organization_UpdateStorage] @OrganizationId
        EXEC [dbo].[User_BumpAccountRevisionDateByCipherId] @Id, @OrganizationId
    END
    ELSE IF @UserId IS NOT NULL
    BEGIN
        EXEC [dbo].[User_UpdateStorage] @UserId
        EXEC [dbo].[User_BumpAccountRevisionDate] @UserId
    END
END
GO

IF OBJECT_ID('[dbo].[Cipher_DeleteById]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Cipher_DeleteById]
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

IF OBJECT_ID('[dbo].[Cipher_Move]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Cipher_Move]
END
GO

CREATE PROCEDURE [dbo].[Cipher_Move]
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
            [dbo].[UserCipherDetails](@UserId)
        WHERE
            [Edit] = 1
            AND [Id] IN (SELECT * FROM @Ids)
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

IF OBJECT_ID('[dbo].[Cipher_Update]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Cipher_Update]
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
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[Cipher]
    SET
        [UserId] = @UserId,
        [OrganizationId] = @OrganizationId,
        [Type] = @Type,
        [Data] = @Data,
        [Favorites] = @Favorites,
        [Folders] = @Folders,
        [Attachments] = @Attachments,
        [CreationDate] = @CreationDate,
        [RevisionDate] = @RevisionDate
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

IF OBJECT_ID('[dbo].[Cipher_UpdateAttachment]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Cipher_UpdateAttachment]
END
GO

CREATE PROCEDURE [dbo].[Cipher_UpdateAttachment]
    @Id UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @AttachmentId VARCHAR(50),
    @AttachmentData NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @AttachmentIdKey VARCHAR(50) = CONCAT('"', @AttachmentId, '"')
    DECLARE @AttachmentIdPath VARCHAR(50) = CONCAT('$.', @AttachmentIdKey)

    UPDATE
        [dbo].[Cipher]
    SET
        [Attachments] = 
            CASE
            WHEN [Attachments] IS NULL THEN
                CONCAT('{', @AttachmentIdKey, ':', @AttachmentData, '}')
            ELSE
                JSON_MODIFY([Attachments], @AttachmentIdPath, JSON_QUERY(@AttachmentData, '$'))
            END
    WHERE
        [Id] = @Id

    IF @OrganizationId IS NOT NULL
    BEGIN
        EXEC [dbo].[Organization_UpdateStorage] @OrganizationId
        EXEC [dbo].[User_BumpAccountRevisionDateByCipherId] @Id, @OrganizationId
    END
    ELSE IF @UserId IS NOT NULL
    BEGIN
        EXEC [dbo].[User_UpdateStorage] @UserId
        EXEC [dbo].[User_BumpAccountRevisionDate] @UserId
    END
END
GO

IF OBJECT_ID('[dbo].[Cipher_UpdateWithCollections]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Cipher_UpdateWithCollections]
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
    @CollectionIds AS [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    CREATE TABLE #AvailableCollections (
        [Id] UNIQUEIDENTIFIER
    )

    INSERT INTO #AvailableCollections
        SELECT
            C.[Id]
        FROM
            [dbo].[Collection] C
        INNER JOIN
            [Organization] O ON O.[Id] = C.[OrganizationId]
        INNER JOIN
            [dbo].[OrganizationUser] OU ON OU.[OrganizationId] = O.[Id] AND OU.[UserId] = @UserId
        LEFT JOIN
            [dbo].[CollectionUser] CU ON OU.[AccessAll] = 0 AND CU.[CollectionId] = C.[Id] AND CU.[OrganizationUserId] = OU.[Id]
        LEFT JOIN
            [dbo].[GroupUser] GU ON CU.[CollectionId] IS NULL AND OU.[AccessAll] = 0 AND GU.[OrganizationUserId] = OU.[Id]
        LEFT JOIN
            [dbo].[Group] G ON G.[Id] = GU.[GroupId]
        LEFT JOIN
            [dbo].[CollectionGroup] CG ON G.[AccessAll] = 0 AND CG.[GroupId] = GU.[GroupId]
        WHERE
            O.[Id] = @OrganizationId
            AND O.[Enabled] = 1
            AND OU.[Status] = 2 -- Confirmed
            AND (
                OU.[AccessAll] = 1
                OR CU.[ReadOnly] = 0
                OR G.[AccessAll] = 1
                OR CG.[ReadOnly] = 0
            )

    IF (SELECT COUNT(1) FROM #AvailableCollections) < 1
    BEGIN
        -- No writable collections available to share with in this organization.
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
        [RevisionDate] = @RevisionDate
        -- No need to update CreationDate, Favorites, Folders, or Type since that data will not change
    WHERE
        [Id] = @Id

    INSERT INTO [dbo].[CollectionCipher]
    (
        [CollectionId],
        [CipherId]
    )
    SELECT
        [Id],
        @Id
    FROM
        @CollectionIds
    WHERE
        [Id] IN (SELECT [Id] FROM #AvailableCollections)

    IF @Attachments IS NOT NULL
    BEGIN
        EXEC [dbo].[Organization_UpdateStorage] @OrganizationId
        EXEC [dbo].[User_UpdateStorage] @UserId
    END

    EXEC [dbo].[User_BumpAccountRevisionDateByCipherId] @Id, @OrganizationId

    SELECT 0 -- 0 = Success
END
GO

IF OBJECT_ID('[dbo].[CollectionCipher_Create]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[CollectionCipher_Create]
END
GO

CREATE PROCEDURE [dbo].[CollectionCipher_Create]
    @CollectionId UNIQUEIDENTIFIER,
    @CipherId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[CollectionCipher]
    (
        [CollectionId],
        [CipherId]
    )
    VALUES
    (
        @CollectionId,
        @CipherId
    )

    DECLARE @OrganizationId UNIQUEIDENTIFIER = (SELECT TOP 1 [OrganizationId] FROM [dbo].[Cipher] WHERE [Id] = @CipherId)
    IF @OrganizationId IS NOT NULL
    BEGIN
        EXEC [dbo].[User_BumpAccountRevisionDateByCollectionId] @CollectionId, @OrganizationId
    END
END
GO

IF OBJECT_ID('[dbo].[CollectionCipher_Delete]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[CollectionCipher_Delete]
END
GO

CREATE PROCEDURE [dbo].[CollectionCipher_Delete]
    @CollectionId UNIQUEIDENTIFIER,
    @CipherId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DELETE
    FROM
        [dbo].[CollectionCipher]
    WHERE
        [CollectionId] = @CollectionId
        AND [CipherId] = @CipherId

    DECLARE @OrganizationId UNIQUEIDENTIFIER = (SELECT TOP 1 [OrganizationId] FROM [dbo].[Cipher] WHERE [Id] = @CipherId)
    IF @OrganizationId IS NOT NULL
    BEGIN
        EXEC [dbo].[User_BumpAccountRevisionDateByCollectionId] @CollectionId, @OrganizationId
    END
END
GO

IF OBJECT_ID('[dbo].[Collection_Create]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Collection_Create]
END
GO

CREATE PROCEDURE [dbo].[Collection_Create]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @Name VARCHAR(MAX),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[Collection]
    (
        [Id],
        [OrganizationId],
        [Name],
        [CreationDate],
        [RevisionDate]
    )
    VALUES
    (
        @Id,
        @OrganizationId,
        @Name,
        @CreationDate,
        @RevisionDate
    )

    EXEC [dbo].[User_BumpAccountRevisionDateByCollectionId] @Id, @OrganizationId
END
GO

IF OBJECT_ID('[dbo].[Collection_CreateWithGroups]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Collection_CreateWithGroups]
END
GO

CREATE PROCEDURE [dbo].[Collection_CreateWithGroups]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @Name VARCHAR(MAX),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @Groups AS [dbo].[SelectionReadOnlyArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    EXEC [dbo].[Collection_Create] @Id, @OrganizationId, @Name, @CreationDate, @RevisionDate

    ;WITH [AvailableGroupsCTE] AS(
        SELECT
            [Id]
        FROM
            [dbo].[Group]
        WHERE
            [OrganizationId] = @OrganizationId
    )
    INSERT INTO [dbo].[CollectionGroup]
    (
        [CollectionId],
        [GroupId],
        [ReadOnly]
    )
    SELECT
        @Id,
        [Id],
        [ReadOnly]
    FROM
        @Groups
    WHERE
        [Id] IN (SELECT [Id] FROM [AvailableGroupsCTE])

    EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationId] @OrganizationId
END
GO

IF OBJECT_ID('[dbo].[Collection_DeleteById]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Collection_DeleteById]
END
GO

CREATE PROCEDURE [dbo].[Collection_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @OrganizationId UNIQUEIDENTIFIER = (SELECT TOP 1 [OrganizationId] FROM [dbo].[Collection] WHERE [Id] = @Id)
    IF @OrganizationId IS NOT NULL
    BEGIN
        EXEC [dbo].[User_BumpAccountRevisionDateByCollectionId] @Id, @OrganizationId
    END

    DELETE
    FROM
        [dbo].[CollectionGroup]
    WHERE
        [CollectionId] = @Id

    DELETE
    FROM
        [dbo].[Collection]
    WHERE
        [Id] = @Id
END
GO

IF OBJECT_ID('[dbo].[Collection_Update]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Collection_Update]
END
GO

CREATE PROCEDURE [dbo].[Collection_Update]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @Name VARCHAR(MAX),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[Collection]
    SET
        [OrganizationId] = @OrganizationId,
        [Name] = @Name,
        [CreationDate] = @CreationDate,
        [RevisionDate] = @RevisionDate
    WHERE
        [Id] = @Id

    EXEC [dbo].[User_BumpAccountRevisionDateByCollectionId] @Id, @OrganizationId
END
GO

IF OBJECT_ID('[dbo].[Collection_UpdateWithGroups]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Collection_UpdateWithGroups]
END
GO

CREATE PROCEDURE [dbo].[Collection_UpdateWithGroups]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @Name VARCHAR(MAX),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @Groups AS [dbo].[SelectionReadOnlyArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    EXEC [dbo].[Collection_Update] @Id, @OrganizationId, @Name, @CreationDate, @RevisionDate

    ;WITH [AvailableGroupsCTE] AS(
        SELECT
            Id
        FROM
            [dbo].[Group]
        WHERE
            OrganizationId = @OrganizationId
    )
    MERGE
        [dbo].[CollectionGroup] AS [Target]
    USING 
        @Groups AS [Source]
    ON
        [Target].[CollectionId] = @Id
        AND [Target].[GroupId] = [Source].[Id]
    WHEN NOT MATCHED BY TARGET
    AND [Source].[Id] IN (SELECT [Id] FROM [AvailableGroupsCTE]) THEN
        INSERT VALUES
        (
            @Id,
            [Source].[Id],
            [Source].[ReadOnly]
        )
    WHEN MATCHED AND [Target].[ReadOnly] != [Source].[ReadOnly] THEN
        UPDATE SET [Target].[ReadOnly] = [Source].[ReadOnly]
    WHEN NOT MATCHED BY SOURCE
    AND [Target].[CollectionId] = @Id THEN
        DELETE
    ;

    EXEC [dbo].[User_BumpAccountRevisionDateByCollectionId] @Id, @OrganizationId
END
GO

IF OBJECT_ID('[dbo].[GroupUser_UpdateUsers]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[GroupUser_UpdateUsers]
END
GO

CREATE PROCEDURE [dbo].[GroupUser_UpdateUsers]
    @GroupId UNIQUEIDENTIFIER,
    @OrganizationUserIds AS [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @OrgId UNIQUEIDENTIFIER = (
        SELECT TOP 1
            [OrganizationId]
        FROM
            [dbo].[Group]
        WHERE
            [Id] = @GroupId
    )

    ;WITH [AvailableUsersCTE] AS(
        SELECT
            [Id]
        FROM
            [dbo].[OrganizationUser]
        WHERE
            [OrganizationId] = @OrgId
    )
    MERGE
        [dbo].[GroupUser] AS [Target]
    USING 
        @OrganizationUserIds AS [Source]
    ON
        [Target].[GroupId] = @GroupId
        AND [Target].[OrganizationUserId] = [Source].[Id]
    WHEN NOT MATCHED BY TARGET
    AND [Source].[Id] IN (SELECT [Id] FROM [AvailableUsersCTE]) THEN
        INSERT VALUES
        (
            @GroupId,
            [Source].[Id]
        )
    WHEN NOT MATCHED BY SOURCE
    AND [Target].[GroupId] = @GroupId
    AND [Target].[OrganizationUserId] IN (SELECT [Id] FROM [AvailableUsersCTE]) THEN
        DELETE
    ;

    EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationId] @OrgId
END
GO

IF OBJECT_ID('[dbo].[Group_CreateWithCollections]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Group_CreateWithCollections]
END
GO

CREATE PROCEDURE [dbo].[Group_CreateWithCollections]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @Name VARCHAR(MAX),
    @AccessAll BIT,
    @ExternalId NVARCHAR(300),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @Collections AS [dbo].[SelectionReadOnlyArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    EXEC [dbo].[Group_Create] @Id, @OrganizationId, @Name, @AccessAll, @ExternalId, @CreationDate, @RevisionDate

    ;WITH [AvailableCollectionsCTE] AS(
        SELECT
            [Id]
        FROM
            [dbo].[Collection]
        WHERE
            [OrganizationId] = @OrganizationId
    )
    INSERT INTO [dbo].[CollectionGroup]
    (
        [CollectionId],
        [GroupId],
        [ReadOnly]
    )
    SELECT
        [Id],
        @Id,
        [ReadOnly]
    FROM
        @Collections
    WHERE
        [Id] IN (SELECT [Id] FROM [AvailableCollectionsCTE])

    EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationId] @OrganizationId
END
GO

IF OBJECT_ID('[dbo].[Group_DeleteById]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Group_DeleteById]
END
GO

CREATE PROCEDURE [dbo].[Group_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @OrganizationId UNIQUEIDENTIFIER = (SELECT TOP 1 [OrganizationId] FROM [dbo].[Group] WHERE [Id] = @Id)
    IF @OrganizationId IS NOT NULL
    BEGIN
        EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationId] @OrganizationId
    END

    DELETE
    FROM
        [dbo].[Group]
    WHERE
        [Id] = @Id
END
GO

IF OBJECT_ID('[dbo].[Group_UpdateWithCollections]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Group_UpdateWithCollections]
END
GO

CREATE PROCEDURE [dbo].[Group_UpdateWithCollections]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @Name VARCHAR(MAX),
    @AccessAll BIT,
    @ExternalId NVARCHAR(300),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @Collections AS [dbo].[SelectionReadOnlyArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    EXEC [dbo].[Group_Update] @Id, @OrganizationId, @Name, @AccessAll, @ExternalId, @CreationDate, @RevisionDate

    ;WITH [AvailableCollectionsCTE] AS(
        SELECT
            Id
        FROM
            [dbo].[Collection]
        WHERE
            OrganizationId = @OrganizationId
    )
    MERGE
        [dbo].[CollectionGroup] AS [Target]
    USING 
        @Collections AS [Source]
    ON
        [Target].[CollectionId] = [Source].[Id]
        AND [Target].[GroupId] = @Id
    WHEN NOT MATCHED BY TARGET
    AND [Source].[Id] IN (SELECT [Id] FROM [AvailableCollectionsCTE]) THEN
        INSERT VALUES
        (
            [Source].[Id],
            @Id,
            [Source].[ReadOnly]
        )
    WHEN MATCHED AND [Target].[ReadOnly] != [Source].[ReadOnly] THEN
        UPDATE SET [Target].[ReadOnly] = [Source].[ReadOnly]
    WHEN NOT MATCHED BY SOURCE
    AND [Target].[GroupId] = @Id THEN
        DELETE
    ;

    EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationId] @OrganizationId
END
GO

IF OBJECT_ID('[dbo].[OrganizationUser_DeleteById]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[OrganizationUser_DeleteById]
END
GO

CREATE PROCEDURE [dbo].[OrganizationUser_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON
    
    EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationUserId] @Id

    DELETE
    FROM
        [dbo].[CollectionUser]
    WHERE
        [OrganizationUserId] = @Id

    DELETE
    FROM
        [dbo].[GroupUser]
    WHERE
        [OrganizationUserId] = @Id

    DELETE
    FROM
        [dbo].[OrganizationUser]
    WHERE
        [Id] = @Id
END
GO
