IF OBJECT_ID('[dbo].[CollectionCipher_UpdateCollectionsAdmin]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[CollectionCipher_UpdateCollectionsAdmin]
END
GO

CREATE PROCEDURE [dbo].[CollectionCipher_UpdateCollectionsAdmin]
    @CipherId UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @CollectionIds AS [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    ;WITH [AvailableCollectionsCTE] AS(
        SELECT
            Id
        FROM
            [dbo].[Collection]
        WHERE
            OrganizationId = @OrganizationId
    ),
    [CollectionCiphersCTE] AS(
        SELECT
            [CollectionId],
            [CipherId]
        FROM
            [dbo].[CollectionCipher]
        WHERE
            [CipherId] = @CipherId
    )
    MERGE
        [CollectionCiphersCTE] AS [Target]
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
    AND [Target].[CipherId] = @CipherId THEN
        DELETE
    ;

    IF @OrganizationId IS NOT NULL
    BEGIN
        EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationId] @OrganizationId
    END
END
GO

IF OBJECT_ID('[dbo].[CollectionCipher_UpdateCollections]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[CollectionCipher_UpdateCollections]
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
            [dbo].[CollectionGroup] CG ON G.[AccessAll] = 0 AND CG.[CollectionId] = C.[Id] AND CG.[GroupId] = GU.[GroupId]
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
    ),
    [CollectionCiphersCTE] AS(
        SELECT
            [CollectionId],
            [CipherId]
        FROM
            [dbo].[CollectionCipher]
        WHERE
            [CipherId] = @CipherId
    )
    MERGE
        [CollectionCiphersCTE] AS [Target]
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
