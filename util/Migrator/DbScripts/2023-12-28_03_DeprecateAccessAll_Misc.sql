-- Flexible Collections: create new V2 sprocs that don't use AccessAll logic
-- Misc sprocs not caught in previous DeprecateAccessAll migrations

-- Folder_DeleteById_V2
CREATE OR ALTER PROCEDURE [dbo].[Folder_DeleteById_V2]
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


-- CollectionUser_ReadByOrganizationUserIds_V2
CREATE OR ALTER PROCEDURE [dbo].[CollectionUser_ReadByOrganizationUserIds_V2]
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

-- OrganizationUserUserDetails_ReadWithCollectionsById_V2
CREATE OR ALTER PROCEDURE [dbo].[OrganizationUserUserDetails_ReadWithCollectionsById_V2]
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
