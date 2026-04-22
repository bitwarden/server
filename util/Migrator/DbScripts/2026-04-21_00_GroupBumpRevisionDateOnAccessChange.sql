-- Bump Group.RevisionDate when group membership or collection-group access is modified via:
-- 1. Group member updates (GroupUser_UpdateUsers)
-- 2. User group updates (GroupUser_UpdateGroups)
-- 3. Group member removal (GroupUser_Delete)
-- 4. Group member additions (GroupUser_AddUsers)
-- 5. Collection update with groups and users (Collection_UpdateWithGroupsAndUsers)
-- 6. Collection update with groups (Collection_UpdateWithGroups)
-- 7. Bulk collection access (Collection_CreateOrUpdateAccessForMany)

CREATE OR ALTER PROCEDURE [dbo].[GroupUser_UpdateUsers]
    @GroupId UNIQUEIDENTIFIER,
    @OrganizationUserIds AS [dbo].[GuidIdArray] READONLY,
    @RevisionDate DATETIME2(7) = NULL
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

    -- Bump RevisionDate on the affected group
    IF @RevisionDate IS NOT NULL
    BEGIN
        UPDATE
            G
        SET
            G.[RevisionDate] = @RevisionDate
        FROM
            [dbo].[Group] G
        WHERE
            G.[Id] = @GroupId
    END

    -- Insert
    INSERT INTO
        [dbo].[GroupUser]
    SELECT
        @GroupId,
        [Source].[Id]
    FROM
        @OrganizationUserIds AS [Source]
    INNER JOIN
        [dbo].[OrganizationUser] OU ON [Source].[Id] = OU.[Id] AND OU.[OrganizationId] = @OrgId
    WHERE
        NOT EXISTS (
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

    EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationId] @OrgId
END
GO

CREATE OR ALTER PROCEDURE [dbo].[GroupUser_UpdateGroups]
    @OrganizationUserId UNIQUEIDENTIFIER,
    @GroupIds AS [dbo].[GuidIdArray] READONLY,
    @RevisionDate DATETIME2(7) = NULL
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

    -- Bump RevisionDate on all affected groups (old + new)
    IF @RevisionDate IS NOT NULL
    BEGIN
        ;WITH [AffectedGroupsCTE] AS (
            SELECT
                [Id]
            FROM
                @GroupIds

            UNION

            SELECT
                GU.[GroupId]
            FROM
                [dbo].[GroupUser] GU
            WHERE
                GU.[OrganizationUserId] = @OrganizationUserId
        )
        UPDATE
            G
        SET
            G.[RevisionDate] = @RevisionDate
        FROM
            [dbo].[Group] G
        WHERE
            G.[OrganizationId] = @OrgId
            AND G.[Id] IN (SELECT [Id] FROM [AffectedGroupsCTE])
    END

    -- Insert
    INSERT INTO
        [dbo].[GroupUser]
    SELECT
        [Source].[Id],
        @OrganizationUserId
    FROM
        @GroupIds [Source]
    INNER JOIN
        [dbo].[Group] G ON G.[Id] = [Source].[Id] AND G.[OrganizationId] = @OrgId
    WHERE
        NOT EXISTS (
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

    EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationUserId] @OrganizationUserId
END
GO

CREATE OR ALTER PROCEDURE [dbo].[GroupUser_Delete]
    @GroupId UNIQUEIDENTIFIER,
    @OrganizationUserId UNIQUEIDENTIFIER,
    @RevisionDate DATETIME2(7) = NULL
AS
BEGIN
    SET NOCOUNT ON

    -- Bump RevisionDate on the affected group
    IF @RevisionDate IS NOT NULL
    BEGIN
        UPDATE
            G
        SET
            G.[RevisionDate] = @RevisionDate
        FROM
            [dbo].[Group] G
        WHERE
            G.[Id] = @GroupId
    END

    DELETE
    FROM
        [dbo].[GroupUser]
    WHERE
        [GroupId] = @GroupId
        AND [OrganizationUserId] = @OrganizationUserId

    EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationUserId] @OrganizationUserId
END
GO

CREATE OR ALTER PROCEDURE [dbo].[GroupUser_AddUsers]
    @GroupId UNIQUEIDENTIFIER,
    @OrganizationUserIds AS [dbo].[GuidIdArray] READONLY,
    @RevisionDate DATETIME2(7) = NULL
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

    -- Bump RevisionDate on the affected group
    IF @RevisionDate IS NOT NULL
    BEGIN
        UPDATE
            G
        SET
            G.[RevisionDate] = @RevisionDate
        FROM
            [dbo].[Group] G
        WHERE
            G.[Id] = @GroupId
    END

    -- Insert
    INSERT INTO
        [dbo].[GroupUser] (GroupId, OrganizationUserId)
    SELECT DISTINCT
        @GroupId,
        [Source].[Id]
    FROM
        @OrganizationUserIds AS [Source]
    INNER JOIN
        [dbo].[OrganizationUser] OU ON [Source].[Id] = OU.[Id] AND OU.[OrganizationId] = @OrgId
    WHERE
        NOT EXISTS (
            SELECT
                1
            FROM
                [dbo].[GroupUser]
            WHERE
                [GroupId] = @GroupId
                AND [OrganizationUserId] = [Source].[Id]
        )

    EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationId] @OrgId
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

    -- Bump RevisionDate on all affected groups (old + new) before modifying CollectionGroup
    ;WITH [AffectedGroupsCTE] AS (
        SELECT
            g.[Id]
        FROM
            @Groups g

        UNION

        SELECT
            CG.[GroupId]
        FROM
            [dbo].[CollectionGroup] CG
        WHERE
            CG.[CollectionId] = @Id
    )
    UPDATE
        G
    SET
        G.[RevisionDate] = @RevisionDate
    FROM
        [dbo].[Group] G
    WHERE
        G.[OrganizationId] = @OrganizationId
        AND G.[Id] IN (SELECT [Id] FROM [AffectedGroupsCTE])

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

CREATE OR ALTER PROCEDURE [dbo].[Collection_UpdateWithGroups]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @Name VARCHAR(MAX),
    @ExternalId NVARCHAR(300),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @Groups AS [dbo].[CollectionAccessSelectionType] READONLY,
    @DefaultUserCollectionEmail NVARCHAR(256) = NULL,
    @Type TINYINT = 0
AS
BEGIN
    SET NOCOUNT ON

    EXEC [dbo].[Collection_Update] @Id, @OrganizationId, @Name, @ExternalId, @CreationDate, @RevisionDate, @DefaultUserCollectionEmail, @Type

    -- Bump RevisionDate on all affected groups (old + new) before modifying CollectionGroup
    ;WITH [AffectedGroupsCTE] AS (
        SELECT
            g.[Id]
        FROM
            @Groups g

        UNION

        SELECT
            CG.[GroupId]
        FROM
            [dbo].[CollectionGroup] CG
        WHERE
            CG.[CollectionId] = @Id
    )
    UPDATE
        G
    SET
        G.[RevisionDate] = @RevisionDate
    FROM
        [dbo].[Group] G
    WHERE
        G.[OrganizationId] = @OrganizationId
        AND G.[Id] IN (SELECT [Id] FROM [AffectedGroupsCTE])

    -- Groups
    -- Delete groups that are no longer in source
    DELETE
        cg
    FROM
        [dbo].[CollectionGroup] cg
    LEFT JOIN
        @Groups g ON cg.GroupId = g.Id
    WHERE
        cg.CollectionId = @Id
        AND g.Id IS NULL;

    -- Update existing groups
    UPDATE
        cg
    SET
        cg.ReadOnly = g.ReadOnly,
        cg.HidePasswords = g.HidePasswords,
        cg.Manage = g.Manage
    FROM
        [dbo].[CollectionGroup] cg
    INNER JOIN
        @Groups g ON cg.GroupId = g.Id
    WHERE
        cg.CollectionId = @Id
        AND (
            cg.ReadOnly != g.ReadOnly
            OR cg.HidePasswords != g.HidePasswords
            OR cg.Manage != g.Manage
        );

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
    FROM
        @Groups g
    INNER JOIN
        [dbo].[Group] grp ON grp.Id = g.Id
    LEFT JOIN
        [dbo].[CollectionGroup] cg ON cg.CollectionId = @Id AND cg.GroupId = g.Id
    WHERE
        grp.OrganizationId = @OrganizationId
        AND cg.CollectionId IS NULL;

    EXEC [dbo].[User_BumpAccountRevisionDateByCollectionId] @Id, @OrganizationId
END
GO

CREATE OR ALTER PROCEDURE [dbo].[Collection_CreateOrUpdateAccessForMany]
    @OrganizationId UNIQUEIDENTIFIER,
    @CollectionIds AS [dbo].[GuidIdArray] READONLY,
    @Groups AS [dbo].[CollectionAccessSelectionType] READONLY,
    @Users AS [dbo].[CollectionAccessSelectionType] READONLY,
    @RevisionDate DATETIME2(7) = NULL
AS
BEGIN
    SET NOCOUNT ON

     -- Groups
    ;WITH [NewCollectionGroups] AS (
        SELECT
            cId.[Id] AS [CollectionId],
            cg.[Id] AS [GroupId],
            cg.[ReadOnly],
            cg.[HidePasswords],
            cg.[Manage]
        FROM
            @Groups AS cg
        CROSS JOIN -- Create a CollectionGroup record for every CollectionId
            @CollectionIds cId
        INNER JOIN
            [dbo].[Group] g ON cg.[Id] = g.[Id]
        WHERE
            g.[OrganizationId] = @OrganizationId
    )
    MERGE
        [dbo].[CollectionGroup] as [Target]
    USING
        [NewCollectionGroups] AS [Source]
    ON
        [Target].[CollectionId] = [Source].[CollectionId]
        AND [Target].[GroupId] = [Source].[GroupId]
    -- Update the target if any values are different from the source
    WHEN MATCHED AND EXISTS(
        SELECT [Source].[ReadOnly], [Source].[HidePasswords], [Source].[Manage]
        EXCEPT
        SELECT [Target].[ReadOnly], [Target].[HidePasswords], [Target].[Manage]
    ) THEN UPDATE SET
        [Target].[ReadOnly] = [Source].[ReadOnly],
        [Target].[HidePasswords] = [Source].[HidePasswords],
        [Target].[Manage] = [Source].[Manage]
    WHEN NOT MATCHED BY TARGET
        THEN INSERT
        (
            [CollectionId],
            [GroupId],
            [ReadOnly],
            [HidePasswords],
            [Manage]
        )
        VALUES
        (
            [Source].[CollectionId],
            [Source].[GroupId],
            [Source].[ReadOnly],
            [Source].[HidePasswords],
            [Source].[Manage]
        );

    -- Users
    ;WITH [NewCollectionUsers] AS (
        SELECT
            cId.[Id] AS [CollectionId],
            cu.[Id] AS [OrganizationUserId],
            cu.[ReadOnly],
            cu.[HidePasswords],
            cu.[Manage]
        FROM
            @Users AS cu
        CROSS JOIN -- Create a CollectionUser record for every CollectionId
            @CollectionIds cId
        INNER JOIN
            [dbo].[OrganizationUser] u ON cu.[Id] = u.[Id]
        WHERE
            u.[OrganizationId] = @OrganizationId
    )
    MERGE
        [dbo].[CollectionUser] as [Target]
    USING
        [NewCollectionUsers] AS [Source]
    ON
        [Target].[CollectionId] = [Source].[CollectionId]
        AND [Target].[OrganizationUserId] = [Source].[OrganizationUserId]
    -- Update the target if any values are different from the source
    WHEN MATCHED AND EXISTS(
        SELECT [Source].[ReadOnly], [Source].[HidePasswords], [Source].[Manage]
        EXCEPT
        SELECT [Target].[ReadOnly], [Target].[HidePasswords], [Target].[Manage]
    ) THEN UPDATE SET
        [Target].[ReadOnly] = [Source].[ReadOnly],
        [Target].[HidePasswords] = [Source].[HidePasswords],
        [Target].[Manage] = [Source].[Manage]
    WHEN NOT MATCHED BY TARGET
        THEN INSERT
        (
            [CollectionId],
            [OrganizationUserId],
            [ReadOnly],
            [HidePasswords],
            [Manage]
        )
        VALUES
        (
            [Source].[CollectionId],
            [Source].[OrganizationUserId],
            [Source].[ReadOnly],
            [Source].[HidePasswords],
            [Source].[Manage]
        );

    IF @RevisionDate IS NOT NULL
    BEGIN
        -- Bump the revision date on all affected collections
        UPDATE
            C
        SET
            C.[RevisionDate] = @RevisionDate
        FROM
            [dbo].[Collection] C
        INNER JOIN
            @CollectionIds CI ON C.[Id] = CI.[Id]

        -- Bump the revision date on all affected groups
        UPDATE
            G
        SET
            G.[RevisionDate] = @RevisionDate
        FROM
            [dbo].[Group] G
        INNER JOIN
            @Groups GR ON G.[Id] = GR.[Id]
        WHERE
            G.[OrganizationId] = @OrganizationId
    END

    EXEC [dbo].[User_BumpAccountRevisionDateByCollectionIds] @CollectionIds, @OrganizationId
END
GO
