CREATE OR ALTER PROCEDURE [dbo].[Collection_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @OrganizationId UNIQUEIDENTIFIER,
    @Name VARCHAR(MAX),
    @ExternalId NVARCHAR(300),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @DefaultUserCollectionEmail NVARCHAR(256) = NULL,
    @Type TINYINT = 0
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[Collection]
    (
        [Id],
        [OrganizationId],
        [Name],
        [ExternalId],
        [CreationDate],
        [RevisionDate],
        [DefaultUserCollectionEmail],
        [Type]
    )
    VALUES
        (
            @Id,
            @OrganizationId,
            @Name,
            @ExternalId,
            @CreationDate,
            @RevisionDate,
            @DefaultUserCollectionEmail,
            @Type
        )

    EXEC [dbo].[User_BumpAccountRevisionDateByCollectionId] @Id, @OrganizationId
END
GO

CREATE OR ALTER PROCEDURE [dbo].[Collection_ReadByUserId]
@UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        [Id],
        [OrganizationId],
        [Name],
        [CreationDate],
        [RevisionDate],
        [ExternalId],
        MIN([ReadOnly]) AS [ReadOnly],
        MIN([HidePasswords]) AS [HidePasswords],
        MAX([Manage]) AS [Manage],
        [DefaultUserCollectionEmail],
        [Type]
    FROM
        [dbo].[UserCollectionDetails](@UserId)
    GROUP BY
        [Id],
        [OrganizationId],
        [Name],
        [CreationDate],
        [RevisionDate],
        [ExternalId],
        [DefaultUserCollectionEmail],
        [Type]
END
GO

CREATE OR ALTER PROCEDURE [dbo].[Collection_Update]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @Name VARCHAR(MAX),
    @ExternalId NVARCHAR(300),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @DefaultUserCollectionEmail NVARCHAR(256) = NULL,
    @Type TINYINT = 0
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[Collection]
    SET
        [OrganizationId] = @OrganizationId,
        [Name] = @Name,
        [ExternalId] = @ExternalId,
        [CreationDate] = @CreationDate,
        [RevisionDate] = @RevisionDate,
        [DefaultUserCollectionEmail] = @DefaultUserCollectionEmail,
        [Type] = @Type
    WHERE
        [Id] = @Id

    EXEC [dbo].[User_BumpAccountRevisionDateByCollectionId] @Id, @OrganizationId
END
GO

CREATE OR ALTER PROCEDURE [dbo].[Collection_UpdateWithGroupsAndUsers]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @Name VARCHAR(MAX),
    @ExternalId NVARCHAR(300),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @Groups AS [dbo].[CollectionAccessSelectionType] READONLY,
    @Users AS [dbo].[CollectionAccessSelectionType] READONLY,
    @DefaultUserCollectionEmail NVARCHAR(256) = NULL,
    @Type TINYINT = 0
AS
BEGIN
    SET NOCOUNT ON

    EXEC [dbo].[Collection_Update] @Id, @OrganizationId, @Name, @ExternalId, @CreationDate, @RevisionDate, @DefaultUserCollectionEmail, @Type

    -- Groups
    -- Delete groups that are no longer in source
    DELETE cg
    FROM [dbo].[CollectionGroup] cg
             LEFT JOIN @Groups g ON cg.GroupId = g.Id
    WHERE cg.CollectionId = @Id
      AND g.Id IS NULL;

    -- Update existing groups
    UPDATE cg
    SET cg.ReadOnly = g.ReadOnly,
        cg.HidePasswords = g.HidePasswords,
        cg.Manage = g.Manage
    FROM [dbo].[CollectionGroup] cg
             INNER JOIN @Groups g ON cg.GroupId = g.Id
    WHERE cg.CollectionId = @Id
      AND (cg.ReadOnly != g.ReadOnly
        OR cg.HidePasswords != g.HidePasswords
        OR cg.Manage != g.Manage);

    -- Insert new groups
    INSERT INTO [dbo].[CollectionGroup]
    (
        [CollectionId],
        [GroupId],
        [ReadOnly],
        [HidePasswords],
        [Manage]
    )
    SELECT
        @Id,
        g.Id,
        g.ReadOnly,
        g.HidePasswords,
        g.Manage
    FROM @Groups g
             INNER JOIN [dbo].[Group] grp ON grp.Id = g.Id
             LEFT JOIN [dbo].[CollectionGroup] cg
                       ON cg.CollectionId = @Id AND cg.GroupId = g.Id
    WHERE grp.OrganizationId = @OrganizationId
      AND cg.CollectionId IS NULL;

    -- Users
    -- Delete users that are no longer in source
    DELETE cu
    FROM [dbo].[CollectionUser] cu
             LEFT JOIN @Users u ON cu.OrganizationUserId = u.Id
    WHERE cu.CollectionId = @Id
      AND u.Id IS NULL;

    -- Update existing users
    UPDATE cu
    SET cu.ReadOnly = u.ReadOnly,
        cu.HidePasswords = u.HidePasswords,
        cu.Manage = u.Manage
    FROM [dbo].[CollectionUser] cu
             INNER JOIN @Users u ON cu.OrganizationUserId = u.Id
    WHERE cu.CollectionId = @Id
      AND (cu.ReadOnly != u.ReadOnly
        OR cu.HidePasswords != u.HidePasswords
        OR cu.Manage != u.Manage);

    -- Insert new users
    INSERT INTO [dbo].[CollectionUser]
    (
        [CollectionId],
        [OrganizationUserId],
        [ReadOnly],
        [HidePasswords],
        [Manage]
    )
    SELECT
        @Id,
        u.Id,
        u.ReadOnly,
        u.HidePasswords,
        u.Manage
    FROM @Users u
             INNER JOIN [dbo].[OrganizationUser] ou ON ou.Id = u.Id
             LEFT JOIN [dbo].[CollectionUser] cu
                       ON cu.CollectionId = @Id AND cu.OrganizationUserId = u.Id
    WHERE ou.OrganizationId = @OrganizationId
      AND cu.CollectionId IS NULL;

    EXEC [dbo].[User_BumpAccountRevisionDateByCollectionId] @Id, @OrganizationId
END
GO

CREATE OR ALTER PROCEDURE [dbo].[Collection_ReadByOrganizationIdWithPermissions]
    @OrganizationId UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER,
    @IncludeAccessRelationships BIT
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        C.*,
        MIN(CASE
                WHEN
                    COALESCE(CU.[ReadOnly], CG.[ReadOnly], 0) = 0
                    THEN 0
                ELSE 1
            END) AS [ReadOnly],
        MIN(CASE
                WHEN
                    COALESCE(CU.[HidePasswords], CG.[HidePasswords], 0) = 0
                    THEN 0
                ELSE 1
            END) AS [HidePasswords],
        MAX(CASE
                WHEN
                    COALESCE(CU.[Manage], CG.[Manage], 0) = 0
                    THEN 0
                ELSE 1
            END) AS [Manage],
        MAX(CASE
                WHEN
                    CU.[CollectionId] IS NULL AND CG.[CollectionId] IS NULL
                    THEN 0
                ELSE 1
            END) AS [Assigned],
        CASE
            WHEN
                -- No user or group has manage rights
                NOT EXISTS(
                    SELECT 1
                    FROM [dbo].[CollectionUser] CU2
                             JOIN [dbo].[OrganizationUser] OU2 ON CU2.[OrganizationUserId] = OU2.[Id]
                    WHERE
                        CU2.[CollectionId] = C.[Id] AND
                        CU2.[Manage] = 1
                )
                    AND NOT EXISTS (
                    SELECT 1
                    FROM [dbo].[CollectionGroup] CG2
                    WHERE
                        CG2.[CollectionId] = C.[Id] AND
                        CG2.[Manage] = 1
                )
                THEN 1
            ELSE 0
            END AS [Unmanaged]
    FROM
        [dbo].[CollectionView] C
            LEFT JOIN
        [dbo].[OrganizationUser] OU ON C.[OrganizationId] = OU.[OrganizationId] AND OU.[UserId] = @UserId
            LEFT JOIN
        [dbo].[CollectionUser] CU ON CU.[CollectionId] = C.[Id] AND CU.[OrganizationUserId] = [OU].[Id]
            LEFT JOIN
        [dbo].[GroupUser] GU ON CU.[CollectionId] IS NULL AND GU.[OrganizationUserId] = OU.[Id]
            LEFT JOIN
        [dbo].[Group] G ON G.[Id] = GU.[GroupId]
            LEFT JOIN
        [dbo].[CollectionGroup] CG ON CG.[CollectionId] = C.[Id] AND CG.[GroupId] = GU.[GroupId]
    WHERE
        C.[OrganizationId] = @OrganizationId
    GROUP BY
        C.[Id],
        C.[OrganizationId],
        C.[Name],
        C.[CreationDate],
        C.[RevisionDate],
        C.[ExternalId],
        C.[DefaultUserCollectionEmail],
        C.[Type]

    IF (@IncludeAccessRelationships = 1)
        BEGIN
            EXEC [dbo].[CollectionGroup_ReadByOrganizationId] @OrganizationId
            EXEC [dbo].[CollectionUser_ReadByOrganizationId] @OrganizationId
        END
END
GO

CREATE OR ALTER PROCEDURE [dbo].[Collection_CreateWithGroupsAndUsers]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @Name VARCHAR(MAX),
    @ExternalId NVARCHAR(300),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @Groups AS [dbo].[CollectionAccessSelectionType] READONLY,
    @Users AS [dbo].[CollectionAccessSelectionType] READONLY,
    @DefaultUserCollectionEmail NVARCHAR(256) = NULL,
    @Type TINYINT = 0
AS
BEGIN
    SET NOCOUNT ON

    EXEC [dbo].[Collection_Create] @Id, @OrganizationId, @Name, @ExternalId, @CreationDate, @RevisionDate, @DefaultUserCollectionEmail, @Type

    -- Groups
    ;WITH [AvailableGroupsCTE] AS(
        SELECT
            [Id]
        FROM
            [dbo].[Group]
        WHERE
            [OrganizationId] = @OrganizationId
    )
     INSERT INTO [dbo].[CollectionGroup]
     (
         [CollectionId],
         [GroupId],
         [ReadOnly],
         [HidePasswords],
         [Manage]
     )
     SELECT
         @Id,
         [Id],
         [ReadOnly],
         [HidePasswords],
         [Manage]
     FROM
         @Groups
     WHERE
         [Id] IN (SELECT [Id] FROM [AvailableGroupsCTE])

    -- Users
    ;WITH [AvailableUsersCTE] AS(
        SELECT
            [Id]
        FROM
            [dbo].[OrganizationUser]
        WHERE
            [OrganizationId] = @OrganizationId
    )
     INSERT INTO [dbo].[CollectionUser]
     (
         [CollectionId],
         [OrganizationUserId],
         [ReadOnly],
         [HidePasswords],
         [Manage]
     )
     SELECT
         @Id,
         [Id],
         [ReadOnly],
         [HidePasswords],
         [Manage]
     FROM
         @Users
     WHERE
         [Id] IN (SELECT [Id] FROM [AvailableUsersCTE])

    EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationId] @OrganizationId
END
GO

CREATE OR ALTER PROCEDURE [dbo].[Collection_ReadByIdWithPermissions]
    @CollectionId UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER,
    @IncludeAccessRelationships BIT
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        C.*,
        MIN(CASE
                WHEN
                    COALESCE(CU.[ReadOnly], CG.[ReadOnly], 0) = 0
                    THEN 0
                ELSE 1
            END) AS [ReadOnly],
        MIN (CASE
                 WHEN
                     COALESCE(CU.[HidePasswords], CG.[HidePasswords], 0) = 0
                     THEN 0
                 ELSE 1
            END) AS [HidePasswords],
        MAX(CASE
                WHEN
                    COALESCE(CU.[Manage], CG.[Manage], 0) = 0
                    THEN 0
                ELSE 1
            END) AS [Manage],
        MAX(CASE
                WHEN
                    CU.[CollectionId] IS NULL AND CG.[CollectionId] IS NULL
                    THEN 0
                ELSE 1
            END) AS [Assigned],
        CASE
            WHEN
                -- No user or group has manage rights
                NOT EXISTS(
                    SELECT 1
                    FROM [dbo].[CollectionUser] CU2
                             JOIN [dbo].[OrganizationUser] OU2 ON CU2.[OrganizationUserId] = OU2.[Id]
                    WHERE
                        CU2.[CollectionId] = C.[Id] AND
                        CU2.[Manage] = 1
                )
                    AND NOT EXISTS (
                    SELECT 1
                    FROM [dbo].[CollectionGroup] CG2
                    WHERE
                        CG2.[CollectionId] = C.[Id] AND
                        CG2.[Manage] = 1
                )
                THEN 1
            ELSE 0
            END AS [Unmanaged]
    FROM
        [dbo].[CollectionView] C
            LEFT JOIN
        [dbo].[OrganizationUser] OU ON C.[OrganizationId] = OU.[OrganizationId] AND OU.[UserId] = @UserId
            LEFT JOIN
        [dbo].[CollectionUser] CU ON CU.[CollectionId] = C.[Id] AND CU.[OrganizationUserId] = [OU].[Id]
            LEFT JOIN
        [dbo].[GroupUser] GU ON CU.[CollectionId] IS NULL AND GU.[OrganizationUserId] = OU.[Id]
            LEFT JOIN
        [dbo].[Group] G ON G.[Id] = GU.[GroupId]
            LEFT JOIN
        [dbo].[CollectionGroup] CG ON CG.[CollectionId] = C.[Id] AND CG.[GroupId] = GU.[GroupId]
    WHERE
        C.[Id] = @CollectionId
    GROUP BY
        C.[Id],
        C.[OrganizationId],
        C.[Name],
        C.[CreationDate],
        C.[RevisionDate],
        C.[ExternalId],
        C.[DefaultUserCollectionEmail],
        C.[Type]

    IF (@IncludeAccessRelationships = 1)
        BEGIN
            EXEC [dbo].[CollectionGroup_ReadByCollectionId] @CollectionId
            EXEC [dbo].[CollectionUser_ReadByCollectionId] @CollectionId
        END
END
