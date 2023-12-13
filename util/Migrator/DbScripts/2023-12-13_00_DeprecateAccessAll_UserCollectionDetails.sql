-- Flexible Collections: create new UserCollectionDetails function that doesn't use AccessAll logic

CREATE OR ALTER FUNCTION [dbo].[UserCollectionDetails_V2](@UserId UNIQUEIDENTIFIER)
RETURNS TABLE
AS RETURN
SELECT
    C.*,
    CASE
        WHEN
            COALESCE(CU.[ReadOnly], CG.[ReadOnly], 0) = 0
        THEN 0
        ELSE 1
    END [ReadOnly],
    CASE
        WHEN
            COALESCE(CU.[HidePasswords], CG.[HidePasswords], 0) = 0
        THEN 0
        ELSE 1
    END [HidePasswords],
    CASE
        WHEN
            COALESCE(CU.[Manage], CG.[Manage], 0) = 0
        THEN 0
        ELSE 1
    END [Manage]
FROM
    [dbo].[CollectionView] C
INNER JOIN
    [dbo].[OrganizationUser] OU ON C.[OrganizationId] = OU.[OrganizationId]
INNER JOIN
    [dbo].[Organization] O ON O.[Id] = C.[OrganizationId]
LEFT JOIN
    [dbo].[CollectionUser] CU ON CU.[CollectionId] = C.[Id] AND CU.[OrganizationUserId] = [OU].[Id]
LEFT JOIN
    [dbo].[GroupUser] GU ON CU.[CollectionId] IS NULL AND GU.[OrganizationUserId] = OU.[Id]
LEFT JOIN
    [dbo].[Group] G ON G.[Id] = GU.[GroupId]
LEFT JOIN
    [dbo].[CollectionGroup] CG ON CG.[CollectionId] = C.[Id] AND CG.[GroupId] = GU.[GroupId]
WHERE
    OU.[UserId] = @UserId
    AND OU.[Status] = 2 -- 2 = Confirmed
    AND O.[Enabled] = 1
    AND (
        CU.[CollectionId] IS NOT NULL
        OR CG.[CollectionId] IS NOT NULL
    )
GO

-- Create v2 sprocs for all sprocs that call UserCollectionDetails

-- Collection_ReadByIdUserId_V2
CREATE OR ALTER PROCEDURE [dbo].[Collection_ReadByIdUserId_V2]
    @Id UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON
    SELECT
        Id,
        OrganizationId,
        [Name],
        CreationDate,
        RevisionDate,
        ExternalId,
        MIN([ReadOnly]) AS [ReadOnly],
        MIN([HidePasswords]) AS [HidePasswords],
        MIN([Manage]) AS [Manage]
    FROM
        [dbo].[UserCollectionDetails_V2](@UserId)
    WHERE
        [Id] = @Id
    GROUP BY
        Id,
        OrganizationId,
        [Name],
        CreationDate,
        RevisionDate,
        ExternalId
END
GO

-- Collection_ReadByUserId_V2
CREATE OR ALTER PROCEDURE [dbo].[Collection_ReadByUserId_V2]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        Id,
        OrganizationId,
        [Name],
        CreationDate,
        RevisionDate,
        ExternalId,
        MIN([ReadOnly]) AS [ReadOnly],
        MIN([HidePasswords]) AS [HidePasswords],
        MIN([Manage]) AS [Manage]
    FROM
        [dbo].[UserCollectionDetails_V2](@UserId)
    GROUP BY
        Id,
        OrganizationId,
        [Name],
        CreationDate,
        RevisionDate,
        ExternalId
END
GO

-- Collection_ReadWithGroupsAndUsersByIdUserId_V2
CREATE OR ALTER PROCEDURE [dbo].[Collection_ReadWithGroupsAndUsersByIdUserId_V2]
    @Id UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    EXEC [dbo].[Collection_ReadByIdUserId_V2] @Id, @UserId

    EXEC [dbo].[CollectionGroup_ReadByCollectionId] @Id

    EXEC [dbo].[CollectionUser_ReadByCollectionId] @Id
END
GO

-- Collection_ReadWithGroupsAndUsersByUserId_V2
CREATE OR ALTER PROCEDURE [dbo].[Collection_ReadWithGroupsAndUsersByUserId_V2]
	@UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @TempUserCollections TABLE(
        Id UNIQUEIDENTIFIER,
        OrganizationId UNIQUEIDENTIFIER,
        Name VARCHAR(MAX),
        CreationDate DATETIME2(7),
        RevisionDate DATETIME2(7),
        ExternalId NVARCHAR(300),
        ReadOnly BIT,
        HidePasswords BIT,
        Manage BIT)

    INSERT INTO @TempUserCollections EXEC [dbo].[Collection_ReadByUserId_V2] @UserId

    SELECT
        *
    FROM
        @TempUserCollections C

    SELECT
        CG.*
    FROM
        [dbo].[CollectionGroup] CG
    INNER JOIN
        @TempUserCollections C ON C.[Id] = CG.[CollectionId]

    SELECT
        CU.*
    FROM
        [dbo].[CollectionUser] CU
    INNER JOIN
        @TempUserCollections C ON C.[Id] = CU.[CollectionId]

END
GO
