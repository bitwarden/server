ALTER TABLE [dbo].[Group] ALTER COLUMN [Name] NVARCHAR (100) NOT NULL
GO

IF OBJECT_ID('[dbo].[UserCollectionDetails]') IS NOT NULL
BEGIN
    DROP FUNCTION [dbo].[UserCollectionDetails]
END
GO

CREATE FUNCTION [dbo].[UserCollectionDetails](@UserId UNIQUEIDENTIFIER)
RETURNS TABLE
AS RETURN
SELECT
    C.*,
    CASE
        WHEN
            OU.[AccessAll] = 1
            OR G.[AccessAll] = 1
            OR CU.[ReadOnly] = 0
            OR CG.[ReadOnly] = 0
        THEN 0
        ELSE 1
    END [ReadOnly]
FROM
    [dbo].[CollectionView] C
INNER JOIN
    [dbo].[OrganizationUser] OU ON C.[OrganizationId] = OU.[OrganizationId]
INNER JOIN
    [dbo].[Organization] O ON O.[Id] = C.[OrganizationId]
LEFT JOIN
    [dbo].[CollectionUser] CU ON OU.[AccessAll] = 0 AND CU.[CollectionId] = C.[Id] AND CU.[OrganizationUserId] = [OU].[Id]
LEFT JOIN
    [dbo].[GroupUser] GU ON CU.[CollectionId] IS NULL AND OU.[AccessAll] = 0 AND GU.[OrganizationUserId] = OU.[Id]
LEFT JOIN
    [dbo].[Group] G ON G.[Id] = GU.[GroupId]
LEFT JOIN
    [dbo].[CollectionGroup] CG ON G.[AccessAll] = 0 AND CG.[CollectionId] = C.[Id] AND CG.[GroupId] = GU.[GroupId]
WHERE
    OU.[UserId] = @UserId
    AND OU.[Status] = 2 -- 2 = Confirmed
    AND O.[Enabled] = 1
    AND (
        OU.[AccessAll] = 1
        OR CU.[CollectionId] IS NOT NULL
        OR G.[AccessAll] = 1
        OR CG.[CollectionId] IS NOT NULL
    )
GO

IF OBJECT_ID('[dbo].[Collection_ReadByIdUserId]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Collection_ReadByIdUserId]
END
GO

CREATE PROCEDURE [dbo].[Collection_ReadByIdUserId]
    @Id UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON
    SELECT TOP 1
        *
    FROM
        [dbo].[UserCollectionDetails](@UserId)
    WHERE
        [Id] = @Id
    ORDER BY
        [ReadOnly] ASC
END
GO

IF OBJECT_ID('[dbo].[Collection_ReadByUserId]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Collection_ReadByUserId]
END
GO

CREATE PROCEDURE [dbo].[Collection_ReadByUserId]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[UserCollectionDetails](@UserId)
END
GO

IF OBJECT_ID('[dbo].[Collection_ReadWithGroupsByIdUserId]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Collection_ReadWithGroupsByIdUserId]
END
GO

CREATE PROCEDURE [dbo].[Collection_ReadWithGroupsByIdUserId]
    @Id UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    EXEC [dbo].[Collection_ReadByIdUserId] @Id, @UserId

    SELECT
        [GroupId] [Id],
        [ReadOnly]
    FROM
        [dbo].[CollectionGroup]
    WHERE
        [CollectionId] = @Id
END
GO

IF OBJECT_ID('[dbo].[Collection_UpdateUsers]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Collection_UpdateUsers]
END
GO

IF OBJECT_ID('[dbo].[CollectionUser_UpdateUsers]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[CollectionUser_UpdateUsers]
END
GO

CREATE PROCEDURE [dbo].[CollectionUser_UpdateUsers]
    @CollectionId UNIQUEIDENTIFIER,
    @Users AS [dbo].[SelectionReadOnlyArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @OrgId UNIQUEIDENTIFIER = (
        SELECT TOP 1
            [OrganizationId]
        FROM
            [dbo].[Collection]
        WHERE
            [Id] = @CollectionId
    )

    ;WITH [AvailableUsersCTE] AS(
        SELECT
            Id
        FROM
            [dbo].[OrganizationUser]
        WHERE
            OrganizationId = @OrgId
    )
    MERGE
        [dbo].[CollectionUser] AS [Target]
    USING 
        @Users AS [Source]
    ON
        [Target].[CollectionId] = @CollectionId
        AND [Target].[OrganizationUserId] = [Source].[Id]
    WHEN NOT MATCHED BY TARGET
    AND [Source].[Id] IN (SELECT [Id] FROM [AvailableUsersCTE]) THEN
        INSERT VALUES
        (
            @CollectionId,
            [Source].[Id],
            [Source].[ReadOnly]
        )
    WHEN MATCHED AND [Target].[ReadOnly] != [Source].[ReadOnly] THEN
        UPDATE SET [Target].[ReadOnly] = [Source].[ReadOnly]
    WHEN NOT MATCHED BY SOURCE
    AND [Target].[CollectionId] = @CollectionId THEN
        DELETE
    ;

    EXEC [dbo].[User_BumpAccountRevisionDateByCollectionId] @CollectionId, @OrgId
END
GO

IF OBJECT_ID('[dbo].[CollectionUserDetails_ReadByCollectionId]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[CollectionUserDetails_ReadByCollectionId]
END
GO

IF OBJECT_ID('[dbo].[CollectionUser_ReadByCollectionId]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[CollectionUser_ReadByCollectionId]
END
GO

CREATE PROCEDURE [dbo].[CollectionUser_ReadByCollectionId]
    @CollectionId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        [OrganizationUserId] [Id],
        [ReadOnly]
    FROM
        [dbo].[CollectionUser]
    WHERE
        [CollectionId] = @CollectionId
END
GO

IF OBJECT_ID('[dbo].[GroupUserDetails_ReadByGroupId]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[GroupUserDetails_ReadByGroupId]
END
GO

IF OBJECT_ID('[dbo].[GroupUser_ReadOrganizationUserIdsByGroupId]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[GroupUser_ReadOrganizationUserIdsByGroupId]
END
GO

CREATE PROCEDURE [dbo].[GroupUser_ReadOrganizationUserIdsByGroupId]
    @GroupId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        [OrganizationUserId]
    FROM
        [dbo].[GroupUser]
    WHERE
        [GroupId] = @GroupId
END
GO

IF OBJECT_ID('[dbo].[Cipher_UpdateCollections]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Cipher_UpdateCollections]
END
GO

CREATE PROCEDURE [dbo].[Cipher_UpdateCollections]
    @Id UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @CollectionIds AS [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    IF @OrganizationId IS NULL OR (SELECT COUNT(1) FROM @CollectionIds) < 1
    BEGIN
        RETURN(-1)
    END

    CREATE TABLE #AvailableCollections (
        [Id] UNIQUEIDENTIFIER
    )

    IF @UserId IS NULL
    BEGIN
        INSERT INTO #AvailableCollections
            SELECT
                [Id]
            FROM
                [dbo].[Collection]
            WHERE
                [OrganizationId] = @OrganizationId
    END
    ELSE
    BEGIN
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
                [dbo].[CollectionGroup] CG ON G.[AccessAll] = 0 AND CG.[CollectionId] = C.[Id] AND CG.[GroupId] = GU.[GroupId]
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
    END

    IF (SELECT COUNT(1) FROM #AvailableCollections) < 1
    BEGIN
        -- No writable collections available to share with in this organization.
        RETURN(-1)
    END

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

    RETURN(0)
END
GO

IF OBJECT_ID('[dbo].[CipherDetails_CreateWithCollections]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[CipherDetails_CreateWithCollections]
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
    @CollectionIds AS [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    EXEC [dbo].[CipherDetails_Create] @Id, @UserId, @OrganizationId, @Type, @Data, @Favorites, @Folders,
        @Attachments, @CreationDate, @RevisionDate, @FolderId, @Favorite, @Edit, @OrganizationUseTotp

    DECLARE @UpdateCollectionsSuccess INT
    EXEC @UpdateCollectionsSuccess = [dbo].[Cipher_UpdateCollections] @Id, @UserId, @OrganizationId, @CollectionIds
END
GO

IF OBJECT_ID('[dbo].[Cipher_CreateWithCollections]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Cipher_CreateWithCollections]
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
    @CollectionIds AS [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    EXEC [dbo].[Cipher_Create] @Id, @UserId, @OrganizationId, @Type, @Data, @Favorites, @Folders,
        @Attachments, @CreationDate, @RevisionDate

    DECLARE @UpdateCollectionsSuccess INT
    EXEC @UpdateCollectionsSuccess = [dbo].[Cipher_UpdateCollections] @Id, @UserId, @OrganizationId, @CollectionIds
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
        [RevisionDate] = @RevisionDate
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
        CASE WHEN @OrganizationId IS NULL THEN @UserId ELSE NULL END,
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
        [UserId] = CASE WHEN @OrganizationId IS NULL THEN @UserId ELSE NULL END,
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
