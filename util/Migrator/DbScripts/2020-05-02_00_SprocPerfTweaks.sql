/**
 * Performance updates to various sprocs
 */

IF OBJECT_ID('[dbo].[CollectionUser_UpdateUsers]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[CollectionUser_UpdateUsers];
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

    CREATE TABLE #TempAvailableUsers
    (
        [Id] UNIQUEIDENTIFIER NOT NULL
    )

    INSERT INTO #TempAvailableUsers
    SELECT
        [Id]
    FROM
        [dbo].[OrganizationUser]
    WHERE
        [OrganizationId] = @OrgId

    -- Update
    UPDATE
        [Target]
    SET
        [Target].[ReadOnly] = [Source].[ReadOnly]
    FROM
        [dbo].[CollectionUser] AS [Target]
    INNER JOIN
        @Users AS [Source] ON [Source].[Id] = [Target].[OrganizationUserId]
    WHERE
        [Target].[CollectionId] = @CollectionId
        AND [Target].[ReadOnly] != [Source].[ReadOnly]

    -- Insert
    INSERT INTO
        [dbo].[CollectionUser]
    SELECT
        @CollectionId,
        [Source].[Id],
        [Source].[ReadOnly]
    FROM
        @Users AS [Source]
    WHERE
        [Source].[Id] IN (SELECT [Id] FROM #TempAvailableUsers)
        AND NOT EXISTS (
            SELECT
                1
            FROM
                [dbo].[CollectionUser]
            WHERE
                [CollectionId] = @CollectionId
                AND [OrganizationUserId] = [Source].[Id]
        )
    
    -- Delete
    DELETE
        CU
    FROM
        [dbo].[CollectionUser] CU
    WHERE
        CU.[CollectionId] = @CollectionId
        AND NOT EXISTS (
            SELECT
                1
            FROM
                @Users
            WHERE
                [Id] = CU.[OrganizationUserId]
        )

    DROP TABLE #TempAvailableUsers

    EXEC [dbo].[User_BumpAccountRevisionDateByCollectionId] @CollectionId, @OrgId
END
GO

IF OBJECT_ID('[dbo].[GroupUser_UpdateGroups]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[GroupUser_UpdateGroups];
END
GO

CREATE PROCEDURE [dbo].[GroupUser_UpdateGroups]
    @OrganizationUserId UNIQUEIDENTIFIER,
    @GroupIds AS [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @OrgId UNIQUEIDENTIFIER = (
        SELECT TOP 1
            [OrganizationId]
        FROM
            [dbo].[OrganizationUser]
        WHERE
            [Id] = @OrganizationUserId
    )

    CREATE TABLE #TempAvailableGroups
    (
        [Id] UNIQUEIDENTIFIER NOT NULL
    )

    INSERT INTO #TempAvailableGroups
    SELECT
        [Id]
    FROM
        [dbo].[Group]
    WHERE
        [OrganizationId] = @OrgId

    -- Insert
    INSERT INTO
        [dbo].[GroupUser]
    SELECT
        [Source].[Id],
        @OrganizationUserId
    FROM
        @GroupIds AS [Source]
    WHERE
        [Source].[Id] IN (SELECT [Id] FROM #TempAvailableGroups)
        AND NOT EXISTS (
            SELECT
                1
            FROM
                [dbo].[GroupUser]
            WHERE
                [OrganizationUserId] = @OrganizationUserId
                AND [GroupId] = [Source].[Id]
        )
    
    -- Delete
    DELETE
        GU
    FROM
        [dbo].[GroupUser] GU
    WHERE
        GU.[OrganizationUserId] = @OrganizationUserId
        AND NOT EXISTS (
            SELECT
                1
            FROM
                @GroupIds
            WHERE
                [Id] = GU.[GroupId]
        )

    DROP TABLE #TempAvailableGroups

    EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationUserId] @OrganizationUserId
END

IF OBJECT_ID('[dbo].[GroupUser_UpdateUsers]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[GroupUser_UpdateUsers];
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

    CREATE TABLE #TempAvailableUsers
    (
        [Id] UNIQUEIDENTIFIER NOT NULL
    )

    INSERT INTO #TempAvailableUsers
    SELECT
        [Id]
    FROM
        [dbo].[OrganizationUser]
    WHERE
        [OrganizationId] = @OrgId

    -- Insert
    INSERT INTO
        [dbo].[GroupUser]
    SELECT
        @GroupId,
        [Source].[Id]
    FROM
        @OrganizationUserIds AS [Source]
    WHERE
        [Source].[Id] IN (SELECT [Id] FROM #TempAvailableUsers)
        AND NOT EXISTS (
            SELECT
                1
            FROM
                [dbo].[GroupUser]
            WHERE
                [GroupId] = @GroupId
                AND [OrganizationUserId] = [Source].[Id]
        )
    
    -- Delete
    DELETE
        GU
    FROM
        [dbo].[GroupUser] GU
    WHERE
        GU.[GroupId] = @GroupId
        AND NOT EXISTS (
            SELECT
                1
            FROM
                @OrganizationUserIds
            WHERE
                [Id] = GU.[OrganizationUserId]
        )

    DROP TABLE #TempAvailableUsers

    EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationId] @OrgId
END

IF OBJECT_ID('[dbo].[OrganizationUser_UpdateWithCollections]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[OrganizationUser_UpdateWithCollections];
END
GO

CREATE PROCEDURE [dbo].[OrganizationUser_UpdateWithCollections]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER,
    @Email NVARCHAR(50),
    @Key VARCHAR(MAX),
    @Status TINYINT,
    @Type TINYINT,
    @AccessAll BIT,
    @ExternalId NVARCHAR(300),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @Collections AS [dbo].[SelectionReadOnlyArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    EXEC [dbo].[OrganizationUser_Update] @Id, @OrganizationId, @UserId, @Email, @Key, @Status, @Type, @AccessAll, @ExternalId, @CreationDate, @RevisionDate

    CREATE TABLE #TempAvailableCollections
    (
        [Id] UNIQUEIDENTIFIER NOT NULL
    )

    INSERT INTO #TempAvailableCollections
    SELECT
        Id
    FROM
        [dbo].[Collection]
    WHERE
        OrganizationId = @OrganizationId

    -- Update
    UPDATE
        [Target]
    SET
        [Target].[ReadOnly] = [Source].[ReadOnly]
    FROM
        [dbo].[CollectionUser] AS [Target]
    INNER JOIN
        @Collections AS [Source] ON [Source].[Id] = [Target].[CollectionId]
    WHERE
        [Target].[OrganizationUserId] = @Id
        AND [Target].[ReadOnly] != [Source].[ReadOnly]

    -- Insert
    INSERT INTO
        [dbo].[CollectionUser]
    SELECT
        [Source].[Id],
        @Id,
        [Source].[ReadOnly]
    FROM
        @Collections AS [Source]
    WHERE
        [Source].[Id] IN (SELECT [Id] FROM #TempAvailableCollections)
        AND NOT EXISTS (
            SELECT
                1
            FROM
                [dbo].[CollectionUser]
            WHERE
                [CollectionId] = [Source].[Id]
                AND [OrganizationUserId] = @Id
        )
    
    -- Delete
    DELETE
        CU
    FROM
        [dbo].[CollectionUser] CU
    WHERE
        CU.[OrganizationUserId] = @Id
        AND NOT EXISTS (
            SELECT
                1
            FROM
                @Collections
            WHERE
                [Id] = CU.[CollectionId]
        )

    DROP TABLE #TempAvailableCollections
END

IF OBJECT_ID('[dbo].[User_BumpAccountRevisionDateByCipherId]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[User_BumpAccountRevisionDateByCipherId];
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
    INNER JOIN
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
        OU.[OrganizationId] = @OrganizationId
        AND OU.[Status] = 2 -- 2 = Confirmed
        AND (
            CU.[CollectionId] IS NOT NULL
            OR CG.[CollectionId] IS NOT NULL
            OR OU.[AccessAll] = 1
            OR G.[AccessAll] = 1
        )
END

IF OBJECT_ID('[dbo].[User_BumpAccountRevisionDateByCollectionId]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[User_BumpAccountRevisionDateByCollectionId];
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
    INNER JOIN
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
        OU.[OrganizationId] = @OrganizationId
        AND OU.[Status] = 2 -- 2 = Confirmed
        AND (
            CU.[CollectionId] IS NOT NULL
            OR CG.[CollectionId] IS NOT NULL
            OR OU.[AccessAll] = 1
            OR G.[AccessAll] = 1
        )
END
