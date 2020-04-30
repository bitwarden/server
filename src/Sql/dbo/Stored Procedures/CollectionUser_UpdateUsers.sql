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