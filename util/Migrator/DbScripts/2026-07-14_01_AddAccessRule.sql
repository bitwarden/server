-- Add the PAM AccessRule feature: the AccessRule table, its stored procedures, the
-- Collection.AccessRuleId association (column + FK + index + the Collection procs that carry it),
-- and Collection_SetAccessRuleAssociations. Consolidated net-new migration (the feature has not shipped).

-- AccessRule table
IF OBJECT_ID('[dbo].[AccessRule]') IS NULL
BEGIN
    CREATE TABLE [dbo].[AccessRule] (
        [Id]                UNIQUEIDENTIFIER    NOT NULL,
        [OrganizationId]    UNIQUEIDENTIFIER    NOT NULL,
        [Name]              NVARCHAR(256)       NOT NULL,
        [Description]       NVARCHAR(MAX)       NULL,
        [Conditions]        NVARCHAR(MAX)       NOT NULL,
        [SingleActiveLease] BIT                 NOT NULL CONSTRAINT [DF_AccessRule_SingleActiveLease] DEFAULT (0),
        [DefaultLeaseDurationSeconds] INT       NULL,
        [MaxLeaseDurationSeconds]     INT       NULL,
        [Enabled]           BIT                 NOT NULL CONSTRAINT [DF_AccessRule_Enabled] DEFAULT (1),
        [AllowsExtensions]  BIT                 NOT NULL CONSTRAINT [DF_AccessRule_AllowsExtensions] DEFAULT (0),
        [MaxExtensionDurationSeconds] INT       NULL,
        [CreationDate]      DATETIME2(7)        NOT NULL,
        [RevisionDate]      DATETIME2(7)        NOT NULL,
        [LastEditedBy]      UNIQUEIDENTIFIER    NULL,
        CONSTRAINT [PK_AccessRule] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_AccessRule_Organization] FOREIGN KEY ([OrganizationId])
            REFERENCES [dbo].[Organization] ([Id]) ON DELETE CASCADE
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'IX_AccessRule_OrganizationId_Name' AND object_id = OBJECT_ID('[dbo].[AccessRule]'))
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [IX_AccessRule_OrganizationId_Name]
        ON [dbo].[AccessRule] ([OrganizationId] ASC, [Name] ASC);
END
GO

-- Collection.AccessRuleId association
IF COL_LENGTH('[dbo].[Collection]', 'AccessRuleId') IS NULL
BEGIN
    ALTER TABLE [dbo].[Collection]
        ADD [AccessRuleId] UNIQUEIDENTIFIER NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = 'FK_Collection_AccessRule')
BEGIN
    ALTER TABLE [dbo].[Collection]
        ADD CONSTRAINT [FK_Collection_AccessRule] FOREIGN KEY ([AccessRuleId])
            REFERENCES [dbo].[AccessRule] ([Id]) ON DELETE NO ACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'IX_Collection_AccessRuleId' AND object_id = OBJECT_ID('[dbo].[Collection]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Collection_AccessRuleId]
        ON [dbo].[Collection]([AccessRuleId] ASC);
END
GO

-- Refresh CollectionView and the UserCollectionDetails function so they surface the new
-- AccessRuleId column before the read procedures below reference it. A SELECT * view/function
-- does not pick up new base-table columns until refreshed, and UserCollectionDetails
-- (which selects C.* from CollectionView) must be refreshed after the view it depends on.
IF OBJECT_ID('[dbo].[CollectionView]') IS NOT NULL
    BEGIN
        EXECUTE sp_refreshsqlmodule N'[dbo].[CollectionView]';
    END
GO

IF OBJECT_ID('[dbo].[UserCollectionDetails]') IS NOT NULL
    BEGIN
        EXECUTE sp_refreshsqlmodule N'[dbo].[UserCollectionDetails]';
    END
GO

-- AccessRule_Create
CREATE OR ALTER PROCEDURE [dbo].[AccessRule_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @OrganizationId UNIQUEIDENTIFIER,
    @Name NVARCHAR(256),
    @Description NVARCHAR(MAX) = NULL,
    @Conditions NVARCHAR(MAX),
    @SingleActiveLease BIT = 0,
    @DefaultLeaseDurationSeconds INT = NULL,
    @MaxLeaseDurationSeconds INT = NULL,
    @Enabled BIT = 1,
    @AllowsExtensions BIT = 0,
    @MaxExtensionDurationSeconds INT = NULL,
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @LastEditedBy UNIQUEIDENTIFIER = NULL
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[AccessRule]
    (
        [Id],
        [OrganizationId],
        [Name],
        [Description],
        [Conditions],
        [SingleActiveLease],
        [DefaultLeaseDurationSeconds],
        [MaxLeaseDurationSeconds],
        [Enabled],
        [AllowsExtensions],
        [MaxExtensionDurationSeconds],
        [CreationDate],
        [RevisionDate],
        [LastEditedBy]
    )
    VALUES
    (
        @Id,
        @OrganizationId,
        @Name,
        @Description,
        @Conditions,
        @SingleActiveLease,
        @DefaultLeaseDurationSeconds,
        @MaxLeaseDurationSeconds,
        @Enabled,
        @AllowsExtensions,
        @MaxExtensionDurationSeconds,
        @CreationDate,
        @RevisionDate,
        @LastEditedBy
    )
END
GO

-- AccessRule_Update
CREATE OR ALTER PROCEDURE [dbo].[AccessRule_Update]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @Name NVARCHAR(256),
    @Description NVARCHAR(MAX) = NULL,
    @Conditions NVARCHAR(MAX),
    @SingleActiveLease BIT = 0,
    @DefaultLeaseDurationSeconds INT = NULL,
    @MaxLeaseDurationSeconds INT = NULL,
    @Enabled BIT = 1,
    @AllowsExtensions BIT = 0,
    @MaxExtensionDurationSeconds INT = NULL,
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @LastEditedBy UNIQUEIDENTIFIER = NULL
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[AccessRule]
    SET
        [OrganizationId] = @OrganizationId,
        [Name] = @Name,
        [Description] = @Description,
        [Conditions] = @Conditions,
        [SingleActiveLease] = @SingleActiveLease,
        [DefaultLeaseDurationSeconds] = @DefaultLeaseDurationSeconds,
        [MaxLeaseDurationSeconds] = @MaxLeaseDurationSeconds,
        [Enabled] = @Enabled,
        [AllowsExtensions] = @AllowsExtensions,
        [MaxExtensionDurationSeconds] = @MaxExtensionDurationSeconds,
        [CreationDate] = @CreationDate,
        [RevisionDate] = @RevisionDate,
        [LastEditedBy] = @LastEditedBy
    WHERE
        [Id] = @Id
END
GO

-- AccessRule_DeleteById
CREATE OR ALTER PROCEDURE [dbo].[AccessRule_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @OrganizationId UNIQUEIDENTIFIER

    SELECT @OrganizationId = [OrganizationId]
    FROM [dbo].[AccessRule]
    WHERE [Id] = @Id

    IF @OrganizationId IS NULL
    BEGIN
        -- Already gone: idempotent no-op.
        RETURN
    END

    -- Clear the collection links first: the FK Collection.AccessRuleId -> AccessRule is ON DELETE NO ACTION, so the
    -- referencing rows must be detached before the rule can be removed. A cleared collection is simply ungoverned.
    UPDATE [dbo].[Collection]
    SET [AccessRuleId] = NULL,
        [RevisionDate] = SYSUTCDATETIME()
    WHERE [AccessRuleId] = @Id

    DELETE FROM [dbo].[AccessRule]
    WHERE [Id] = @Id

    EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationId] @OrganizationId
END
GO

-- AccessRule_ReadById
CREATE OR ALTER PROCEDURE [dbo].[AccessRule_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT *
    FROM [dbo].[AccessRule]
    WHERE [Id] = @Id
END
GO

-- AccessRule_ReadByOrganizationId
CREATE OR ALTER PROCEDURE [dbo].[AccessRule_ReadByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT *
    FROM [dbo].[AccessRule]
    WHERE [OrganizationId] = @OrganizationId
END
GO

-- AccessRule_ReadDetailsById
CREATE OR ALTER PROCEDURE [dbo].[AccessRule_ReadDetailsById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT *
    FROM [dbo].[AccessRule]
    WHERE [Id] = @Id

    SELECT [Id]
    FROM [dbo].[Collection]
    WHERE [AccessRuleId] = @Id
END
GO

-- AccessRule_ReadDetailsByOrganizationId
CREATE OR ALTER PROCEDURE [dbo].[AccessRule_ReadDetailsByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT *
    FROM [dbo].[AccessRule]
    WHERE [OrganizationId] = @OrganizationId

    SELECT
        [AccessRuleId],
        [Id] AS [CollectionId]
    FROM [dbo].[Collection]
    WHERE [OrganizationId] = @OrganizationId
        AND [AccessRuleId] IS NOT NULL
END
GO

-- Collection_SetAccessRuleAssociations
CREATE OR ALTER PROCEDURE [dbo].[Collection_SetAccessRuleAssociations]
    @AccessRuleId UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @ToAssign AS [dbo].[GuidIdArray] READONLY,
    @ToClear AS [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @RevisionDate DATETIME2(7) = SYSUTCDATETIME()

    BEGIN TRANSACTION

    UPDATE
        C
    SET
        C.[AccessRuleId] = NULL,
        C.[RevisionDate] = @RevisionDate
    FROM
        [dbo].[Collection] C
    INNER JOIN
        @ToClear T ON T.[Id] = C.[Id]
    WHERE
        C.[OrganizationId] = @OrganizationId
        AND C.[AccessRuleId] = @AccessRuleId

    UPDATE
        C
    SET
        C.[AccessRuleId] = @AccessRuleId,
        C.[RevisionDate] = @RevisionDate
    FROM
        [dbo].[Collection] C
    INNER JOIN
        @ToAssign T ON T.[Id] = C.[Id]
    WHERE
        C.[OrganizationId] = @OrganizationId

    COMMIT TRANSACTION

    EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationId] @OrganizationId
END
GO

-- Collection_Create
CREATE OR ALTER PROCEDURE [dbo].[Collection_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @OrganizationId UNIQUEIDENTIFIER,
    @Name VARCHAR(MAX),
    @ExternalId NVARCHAR(300),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @DefaultUserCollectionEmail NVARCHAR(256) = NULL,
    @Type TINYINT = 0,
    @AccessRuleId UNIQUEIDENTIFIER = NULL
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
        [Type],
        [AccessRuleId]
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
        @Type,
        @AccessRuleId
    )

    EXEC [dbo].[User_BumpAccountRevisionDateByCollectionId] @Id, @OrganizationId
END
GO

-- Collection_CreateWithGroupsAndUsers
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
    @Type TINYINT = 0,
    @AccessRuleId UNIQUEIDENTIFIER = NULL
AS
BEGIN
    SET NOCOUNT ON

    EXEC [dbo].[Collection_Create] @Id, @OrganizationId, @Name, @ExternalId, @CreationDate, @RevisionDate, @DefaultUserCollectionEmail, @Type, @AccessRuleId

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

-- Collection_Update
CREATE OR ALTER PROCEDURE [dbo].[Collection_Update]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @Name VARCHAR(MAX),
    @ExternalId NVARCHAR(300),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @DefaultUserCollectionEmail NVARCHAR(256) = NULL,
    @Type TINYINT = 0,
    @AccessRuleId UNIQUEIDENTIFIER = NULL
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
        [Type] = @Type,
        [AccessRuleId] = @AccessRuleId
    WHERE
        [Id] = @Id

    EXEC [dbo].[User_BumpAccountRevisionDateByCollectionId] @Id, @OrganizationId
END
GO

-- Collection_UpdateWithGroups
CREATE OR ALTER PROCEDURE [dbo].[Collection_UpdateWithGroups]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @Name VARCHAR(MAX),
    @ExternalId NVARCHAR(300),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @Groups AS [dbo].[CollectionAccessSelectionType] READONLY,
    @DefaultUserCollectionEmail NVARCHAR(256) = NULL,
    @Type TINYINT = 0,
    @AccessRuleId UNIQUEIDENTIFIER = NULL
AS
BEGIN
    SET NOCOUNT ON

    EXEC [dbo].[Collection_Update] @Id, @OrganizationId, @Name, @ExternalId, @CreationDate, @RevisionDate, @DefaultUserCollectionEmail, @Type, @AccessRuleId

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

-- Collection_UpdateWithGroupsAndUsers
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
    @Type TINYINT = 0,
    @AccessRuleId UNIQUEIDENTIFIER = NULL
AS
BEGIN
    SET NOCOUNT ON

    EXEC [dbo].[Collection_Update] @Id, @OrganizationId, @Name, @ExternalId, @CreationDate, @RevisionDate, @DefaultUserCollectionEmail, @Type, @AccessRuleId

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

-- Collection_UpdateWithUsers
CREATE OR ALTER PROCEDURE [dbo].[Collection_UpdateWithUsers]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @Name VARCHAR(MAX),
    @ExternalId NVARCHAR(300),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @Users AS [dbo].[CollectionAccessSelectionType] READONLY,
    @DefaultUserCollectionEmail NVARCHAR(256) = NULL,
    @Type TINYINT = 0,
    @AccessRuleId UNIQUEIDENTIFIER = NULL
AS
BEGIN
    SET NOCOUNT ON

    EXEC [dbo].[Collection_Update] @Id, @OrganizationId, @Name, @ExternalId, @CreationDate, @RevisionDate, @DefaultUserCollectionEmail, @Type, @AccessRuleId

    -- Users
    -- Delete users that are no longer in source
    DELETE
        cu
    FROM
        [dbo].[CollectionUser] cu
    LEFT JOIN
        @Users u ON cu.OrganizationUserId = u.Id
    WHERE
        cu.CollectionId = @Id
        AND u.Id IS NULL;

    -- Update existing users
    UPDATE
        cu
    SET
        cu.ReadOnly = u.ReadOnly,
        cu.HidePasswords = u.HidePasswords,
        cu.Manage = u.Manage
    FROM
        [dbo].[CollectionUser] cu
    INNER JOIN
        @Users u ON cu.OrganizationUserId = u.Id
    WHERE
        cu.CollectionId = @Id
        AND (
            cu.ReadOnly != u.ReadOnly
            OR cu.HidePasswords != u.HidePasswords
            OR cu.Manage != u.Manage
        );

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
    FROM
        @Users u
    INNER JOIN
        [dbo].[OrganizationUser] ou ON ou.Id = u.Id
    LEFT JOIN
        [dbo].[CollectionUser] cu ON cu.CollectionId = @Id AND cu.OrganizationUserId = u.Id
    WHERE
        ou.OrganizationId = @OrganizationId
        AND cu.CollectionId IS NULL;

    EXEC [dbo].[User_BumpAccountRevisionDateByCollectionId] @Id, @OrganizationId
END
GO

-- Collection_ReadByIdWithPermissions
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
        C.[Type],
        C.[AccessRuleId]

   IF (@IncludeAccessRelationships = 1)
    BEGIN
        EXEC [dbo].[CollectionGroup_ReadByCollectionId] @CollectionId
        EXEC [dbo].[CollectionUser_ReadByCollectionId] @CollectionId
	END
END
GO

-- Collection_ReadByUserId
CREATE OR ALTER PROCEDURE [dbo].[Collection_ReadByUserId]
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
        MAX([Manage]) AS [Manage],
        [DefaultUserCollectionEmail],
        [Type],
        [AccessRuleId]
    FROM
        [dbo].[UserCollectionDetails](@UserId)
    GROUP BY
        Id,
        OrganizationId,
        [Name],
        CreationDate,
        RevisionDate,
        ExternalId,
        [DefaultUserCollectionEmail],
        [Type],
        [AccessRuleId]
END
GO

-- Collection_ReadSharedCollectionsByOrganizationIdWithPermissions
CREATE OR ALTER PROCEDURE [dbo].[Collection_ReadSharedCollectionsByOrganizationIdWithPermissions]
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
        C.[OrganizationId] = @OrganizationId AND
        C.[Type] = 0 -- Only SharedCollection
    GROUP BY
        C.[Id],
        C.[OrganizationId],
        C.[Name],
        C.[CreationDate],
        C.[RevisionDate],
        C.[ExternalId],
        C.[DefaultUserCollectionEmail],
        C.[Type],
        C.[AccessRuleId]

    IF (@IncludeAccessRelationships = 1)
    BEGIN
        EXEC [dbo].[CollectionGroup_ReadByOrganizationId] @OrganizationId
        EXEC [dbo].[CollectionUser_ReadByOrganizationId] @OrganizationId
    END
END
GO

