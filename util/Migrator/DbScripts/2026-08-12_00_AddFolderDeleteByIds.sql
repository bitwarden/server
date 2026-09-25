CREATE OR ALTER PROCEDURE [dbo].[Folder_DeleteByIds]
    @Ids AS [dbo].[GuidIdArray] READONLY,
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @UserIdPath VARCHAR(50) = CONCAT('$."', @UserId, '"')

    DECLARE @OwnedIds AS TABLE ([Id] UNIQUEIDENTIFIER PRIMARY KEY)

    INSERT INTO @OwnedIds ([Id])
    SELECT
        [Id]
    FROM
        [dbo].[Folder]
    WHERE
        [Id] IN (SELECT [Id] FROM @Ids)
        AND [UserId] = @UserId

    IF NOT EXISTS (SELECT 1 FROM @OwnedIds)
    BEGIN
        RETURN
    END

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
        AND EXISTS (
            SELECT
                1
            FROM
                @OwnedIds OI
            WHERE
                OI.[Id] = JSON_VALUE(C.[Folders], @UserIdPath)
        )

    UPDATE
        C
    SET
        C.[Folders] = JSON_MODIFY(C.[Folders], @UserIdPath, NULL)
    FROM
        [dbo].[Cipher] C
    WHERE
        [UserId] = @UserId
        AND EXISTS (
            SELECT
                1
            FROM
                @OwnedIds OI
            WHERE
                OI.[Id] = JSON_VALUE(C.[Folders], @UserIdPath)
        )

    DELETE
    FROM
        [dbo].[Folder]
    WHERE
        [Id] IN (SELECT [Id] FROM @OwnedIds)

    EXEC [dbo].[User_BumpAccountRevisionDate] @UserId
END
GO
