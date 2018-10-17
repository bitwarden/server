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

CREATE PROCEDURE [dbo].[Collection_UpdateUsers]
    @Id UNIQUEIDENTIFIER,
    @Users AS [dbo].[SelectionReadOnlyArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @OrganizationId UNIQUEIDENTIFIER = (SELECT [OrganizationId] FROM [dbo].[Collection] WHERE [Id] = @Id)

    ;WITH [AvailableUsersCTE] AS(
        SELECT
            Id
        FROM
            [dbo].[OrganizationUser]
        WHERE
            OrganizationId = @OrganizationId
    )
    MERGE
        [dbo].[CollectionUser] AS [Target]
    USING 
        @Users AS [Source]
    ON
        [Target].[CollectionId] = @Id
        AND [Target].[OrganizationUserId] = [Source].[Id]
    WHEN NOT MATCHED BY TARGET
    AND [Source].[Id] IN (SELECT [Id] FROM [AvailableUsersCTE]) THEN
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
