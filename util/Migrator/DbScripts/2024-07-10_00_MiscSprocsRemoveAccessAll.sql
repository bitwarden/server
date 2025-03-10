-- Remove AccessAll logic from miscellaneous sprocs

-- CollectionUser_ReadByOrganizationUserIds
CREATE OR ALTER PROCEDURE [dbo].[CollectionUser_ReadByOrganizationUserIds]
    @OrganizationUserIds [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        CU.*
    FROM
        [dbo].[OrganizationUser] OU
    INNER JOIN
        [dbo].[CollectionUser] CU ON CU.[OrganizationUserId] = OU.[Id]
    INNER JOIN
        @OrganizationUserIds OUI ON OUI.[Id] = OU.[Id]
END
GO

-- OrganizationUser_ReadWithCollectionsById
CREATE OR ALTER PROCEDURE [dbo].[OrganizationUser_ReadWithCollectionsById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    EXEC [OrganizationUser_ReadById] @Id

    SELECT
        CU.[CollectionId] Id,
        CU.[ReadOnly],
        CU.[HidePasswords],
        CU.[Manage]
    FROM
        [dbo].[OrganizationUser] OU
    INNER JOIN
        [dbo].[CollectionUser] CU ON CU.[OrganizationUserId] = [OU].[Id]
    WHERE
        [OrganizationUserId] = @Id
END
GO

-- OrganizationUserUserDetails_ReadWithCollectionsById
CREATE OR ALTER PROCEDURE [dbo].[OrganizationUserUserDetails_ReadWithCollectionsById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    EXEC [OrganizationUserUserDetails_ReadById] @Id

    SELECT
        CU.[CollectionId] Id,
        CU.[ReadOnly],
        CU.[HidePasswords],
        CU.[Manage]
    FROM
        [dbo].[OrganizationUser] OU
    INNER JOIN
        [dbo].[CollectionUser] CU ON CU.[OrganizationUserId] = [OU].[Id]
    WHERE
        [OrganizationUserId] = @Id
END
GO

-- Cipher_ReadCanEditByIdUserId
CREATE OR ALTER PROCEDURE [dbo].[Cipher_ReadCanEditByIdUserId]
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

-- Cipher_UpdateCollections
CREATE OR ALTER PROCEDURE [dbo].[Cipher_UpdateCollections]
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

-- Folder_DeleteById
CREATE OR ALTER PROCEDURE [dbo].[Folder_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @UserId UNIQUEIDENTIFIER = (SELECT TOP 1 [UserId] FROM [dbo].[Folder] WHERE [Id] = @Id)
    DECLARE @UserIdPath VARCHAR(50) = CONCAT('$."', @UserId, '"')

    ;WITH [CTE] AS (
        SELECT
            [Id],
            [OrganizationId]
        FROM
            [OrganizationUser]
        WHERE
            [UserId] = @UserId
            AND [Status] = 2 -- Confirmed
    )
    UPDATE
        C
    SET
        C.[Folders] = JSON_MODIFY(C.[Folders], @UserIdPath, NULL)
    FROM
        [dbo].[Cipher] C
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
        (
            CU.[CollectionId] IS NOT NULL
            OR CG.[CollectionId] IS NOT NULL
        )
        AND JSON_VALUE(C.[Folders], @UserIdPath) = @Id

    UPDATE
        C
    SET
        C.[Folders] = JSON_MODIFY(C.[Folders], @UserIdPath, NULL)
    FROM
        [dbo].[Cipher] C
    WHERE
        [UserId] = @UserId
        AND JSON_VALUE([Folders], @UserIdPath) = @Id

    DELETE
    FROM
        [dbo].[Folder]
    WHERE
        [Id] = @Id

    EXEC [dbo].[User_BumpAccountRevisionDate] @UserId
END
GO
