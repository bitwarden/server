IF OBJECT_ID('[dbo].[Collection_ReadByUserId]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Collection_ReadByUserId]
END
GO

IF OBJECT_ID('[dbo].[Cipher_UpdateWithCollections]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Cipher_UpdateWithCollections]
END
GO

IF OBJECT_ID('[dbo].[CollectionCipher_UpdateCollections]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[CollectionCipher_UpdateCollections]
END
GO

CREATE PROCEDURE [dbo].[Collection_ReadByUserId]
    @UserId UNIQUEIDENTIFIER,
    @WriteOnly BIT
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        C.*
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
        AND (
            @WriteOnly = 0
            OR OU.[AccessAll] = 1
            OR G.[AccessAll] = 1
            OR CU.[ReadOnly] = 0
            OR CG.[ReadOnly] = 0
        )
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

    EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationId] @OrganizationId

    SELECT 0 -- 0 = Success
END
GO

CREATE PROCEDURE [dbo].[CollectionCipher_UpdateCollections]
    @CipherId UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER,
    @CollectionIds AS [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @OrgId UNIQUEIDENTIFIER = (
        SELECT TOP 1
            [OrganizationId]
        FROM
            [dbo].[Cipher]
        WHERE
            [Id] = @CipherId
    )

    ;WITH [AvailableCollectionsCTE] AS(
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
            O.[Id] = @OrgId
            AND O.[Enabled] = 1
            AND OU.[Status] = 2 -- Confirmed
            AND (
                OU.[AccessAll] = 1
                OR CU.[ReadOnly] = 0
                OR G.[AccessAll] = 1
                OR CG.[ReadOnly] = 0
            )
    )
    MERGE
        [dbo].[CollectionCipher] AS [Target]
    USING 
        @CollectionIds AS [Source]
    ON
        [Target].[CollectionId] = [Source].[Id]
        AND [Target].[CipherId] = @CipherId
    WHEN NOT MATCHED BY TARGET
    AND [Source].[Id] IN (SELECT [Id] FROM [AvailableCollectionsCTE]) THEN
        INSERT VALUES
        (
            [Source].[Id],
            @CipherId
        )
    WHEN NOT MATCHED BY SOURCE
    AND [Target].[CipherId] = @CipherId
    AND [Target].[CollectionId] IN (SELECT [Id] FROM [AvailableCollectionsCTE]) THEN
        DELETE
    ;

    IF @OrgId IS NOT NULL
    BEGIN
        EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationId] @OrgId
    END
END
GO
