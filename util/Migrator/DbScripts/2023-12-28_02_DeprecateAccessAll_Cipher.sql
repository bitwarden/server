-- Flexible Collections: create new Cipher_UpdateCollections sproc that doesn't use AccessAll logic

CREATE OR ALTER PROCEDURE [dbo].[Cipher_UpdateCollections_V2]
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
                [dbo].[CollectionUser] CU ON CU.[CollectionId] = C.[Id] AND CU.[OrganizationUserId] = OU.[Id]
            LEFT JOIN
                [dbo].[GroupUser] GU ON CU.[CollectionId] IS NULL AND GU.[OrganizationUserId] = OU.[Id]
            LEFT JOIN
                [dbo].[Group] G ON G.[Id] = GU.[GroupId]
            LEFT JOIN
                [dbo].[CollectionGroup] CG ON CG.[CollectionId] = C.[Id] AND CG.[GroupId] = GU.[GroupId]
            WHERE
                O.[Id] = @OrganizationId
                AND O.[Enabled] = 1
                AND OU.[Status] = 2 -- Confirmed
                AND (
                    CU.[ReadOnly] = 0
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

-- Make corresponding V2 versions of all sprocs that call the above

-- CipherDetails_CreateWithCollections_V2
CREATE OR ALTER PROCEDURE [dbo].[CipherDetails_CreateWithCollections_V2]
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
    @ViewPassword BIT, -- not used
    @OrganizationUseTotp BIT, -- not used
    @DeletedDate DATETIME2(7),
    @Reprompt TINYINT,
    @Key VARCHAR(MAX) = NULL,
    @CollectionIds AS [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    EXEC [dbo].[CipherDetails_Create] @Id, @UserId, @OrganizationId, @Type, @Data, @Favorites, @Folders,
        @Attachments, @CreationDate, @RevisionDate, @FolderId, @Favorite, @Edit, @ViewPassword,
        @OrganizationUseTotp, @DeletedDate, @Reprompt, @Key

    DECLARE @UpdateCollectionsSuccess INT
    EXEC @UpdateCollectionsSuccess = [dbo].[Cipher_UpdateCollections_V2] @Id, @UserId, @OrganizationId, @CollectionIds
END
GO


-- Cipher_CreateWithCollections_V2
CREATE OR ALTER PROCEDURE [dbo].[Cipher_CreateWithCollections_V2]
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
    @Reprompt TINYINT,
    @Key VARCHAR(MAX) = NULL,
    @CollectionIds AS [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    EXEC [dbo].[Cipher_Create] @Id, @UserId, @OrganizationId, @Type, @Data, @Favorites, @Folders,
        @Attachments, @CreationDate, @RevisionDate, @DeletedDate, @Reprompt, @Key

    DECLARE @UpdateCollectionsSuccess INT
    EXEC @UpdateCollectionsSuccess = [dbo].[Cipher_UpdateCollections_V2] @Id, @UserId, @OrganizationId, @CollectionIds
END
GO


-- Cipher_UpdateWithCollections_V2
CREATE OR ALTER PROCEDURE [dbo].[Cipher_UpdateWithCollections_V2]
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
    @Reprompt TINYINT,
    @Key VARCHAR(MAX) = NULL,
    @CollectionIds AS [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    BEGIN TRANSACTION Cipher_UpdateWithCollections

    DECLARE @UpdateCollectionsSuccess INT
    EXEC @UpdateCollectionsSuccess = [dbo].[Cipher_UpdateCollections_V2] @Id, @UserId, @OrganizationId, @CollectionIds

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
        [DeletedDate] = @DeletedDate,
        [Key] = @Key
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

-- Cipher_ReadCanEditByIdUserId_V2
-- Doesn't call Cipher_UpdateCollections but also needs updating
CREATE OR ALTER PROCEDURE [dbo].[Cipher_ReadCanEditByIdUserId_V2]
    @Id UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @CanEdit BIT

    ;WITH [CTE] AS (
        SELECT
            CASE
                WHEN C.[UserId] IS NOT NULL OR CU.[ReadOnly] = 0 OR CG.[ReadOnly] = 0 THEN 1
                ELSE 0
            END [Edit]
        FROM
            [dbo].[Cipher] C
        LEFT JOIN
            [dbo].[Organization] O ON C.[UserId] IS NULL AND O.[Id] = C.[OrganizationId]
        LEFT JOIN
            [dbo].[OrganizationUser] OU ON OU.[OrganizationId] = O.[Id] AND OU.[UserId] = @UserId
        LEFT JOIN
            [dbo].[CollectionCipher] CC ON C.[UserId] IS NULL AND CC.[CipherId] = C.[Id]
        LEFT JOIN
            [dbo].[CollectionUser] CU ON CU.[CollectionId] = CC.[CollectionId] AND CU.[OrganizationUserId] = OU.[Id]
        LEFT JOIN
            [dbo].[GroupUser] GU ON C.[UserId] IS NULL AND CU.[CollectionId] IS NULL AND GU.[OrganizationUserId] = OU.[Id]
        LEFT JOIN
            [dbo].[Group] G ON G.[Id] = GU.[GroupId]
        LEFT JOIN
            [dbo].[CollectionGroup] CG ON CG.[CollectionId] = CC.[CollectionId] AND CG.[GroupId] = GU.[GroupId]
        WHERE
            C.Id = @Id
            AND (
                C.[UserId] = @UserId
                OR (
                    C.[UserId] IS NULL
                    AND OU.[Status] = 2 -- 2 = Confirmed
                    AND O.[Enabled] = 1
                    AND (
                        CU.[CollectionId] IS NOT NULL
                        OR CG.[CollectionId] IS NOT NULL
                    )
                )
            )
    )
    SELECT
        @CanEdit = CASE
            WHEN COUNT(1) > 0 THEN 1
            ELSE 0
        END
    FROM
        [CTE]
    WHERE
        [Edit] = 1

    SELECT @CanEdit
END
GO
