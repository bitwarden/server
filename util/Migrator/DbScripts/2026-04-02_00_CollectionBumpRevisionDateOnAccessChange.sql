-- Bump Collection.RevisionDate when collection access is modified via:
-- 1. Bulk collection access (Collection_CreateOrUpdateAccessForMany)
-- 2. Organization user create (OrganizationUser_CreateWithCollections)
-- 3. Organization user update (OrganizationUser_UpdateWithCollections)
-- 4. Bulk organization user create (OrganizationUser_CreateManyWithCollectionsAndGroups)
-- 5. Group create (Group_CreateWithCollections)
-- 6. Group update (Group_UpdateWithCollections)

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
        UPDATE C
        SET C.[RevisionDate] = @RevisionDate
        FROM [dbo].[Collection] C
        INNER JOIN @CollectionIds CI ON C.[Id] = CI.[Id]
    END

    EXEC [dbo].[User_BumpAccountRevisionDateByCollectionIds] @CollectionIds, @OrganizationId
END
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationUser_UpdateWithCollections]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER,
    @Email NVARCHAR(256),
    @Key VARCHAR(MAX),
    @Status SMALLINT,
    @Type TINYINT,
    @ExternalId NVARCHAR(300),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @Permissions NVARCHAR(MAX),
    @ResetPasswordKey VARCHAR(MAX),
    @Collections AS [dbo].[CollectionAccessSelectionType] READONLY,
    @AccessSecretsManager BIT = 0
AS
BEGIN
    SET NOCOUNT ON

    EXEC [dbo].[OrganizationUser_Update] @Id, @OrganizationId, @UserId, @Email, @Key, @Status, @Type, @ExternalId, @CreationDate, @RevisionDate, @Permissions, @ResetPasswordKey, @AccessSecretsManager

    -- Bump RevisionDate on all affected collections
    UPDATE C
    SET C.[RevisionDate] = @RevisionDate
    FROM [dbo].[Collection] C
    WHERE C.[OrganizationId] = @OrganizationId
    AND (
        C.[Id] IN (SELECT [Id] FROM @Collections) -- New/updated assignments
        OR C.[Id] IN (
            SELECT CU.[CollectionId]
            FROM [dbo].[CollectionUser] CU
            WHERE CU.[OrganizationUserId] = @Id -- Existing assignments (includes ones being removed)
        )
    )

    -- Update
    UPDATE
        [Target]
    SET
        [Target].[ReadOnly] = [Source].[ReadOnly],
        [Target].[HidePasswords] = [Source].[HidePasswords],
        [Target].[Manage] = [Source].[Manage]
    FROM
        [dbo].[CollectionUser] AS [Target]
    INNER JOIN
        @Collections AS [Source] ON [Source].[Id] = [Target].[CollectionId]
    WHERE
        [Target].[OrganizationUserId] = @Id
        AND (
            [Target].[ReadOnly] != [Source].[ReadOnly]
            OR [Target].[HidePasswords] != [Source].[HidePasswords]
            OR [Target].[Manage] != [Source].[Manage]
        )

    -- Insert
    INSERT INTO [dbo].[CollectionUser]
    (
        [CollectionId],
        [OrganizationUserId],
        [ReadOnly],
        [HidePasswords],
        [Manage]
    )
    SELECT
        [Source].[Id],
        @Id,
        [Source].[ReadOnly],
        [Source].[HidePasswords],
        [Source].[Manage]
    FROM
        @Collections AS [Source]
    INNER JOIN
        [dbo].[Collection] C ON C.[Id] = [Source].[Id] AND C.[OrganizationId] = @OrganizationId
    WHERE
        NOT EXISTS (
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
    INNER JOIN
        [dbo].[Collection] C ON C.[Id] = CU.[CollectionId]
    WHERE
        CU.[OrganizationUserId] = @Id
        AND C.[Type] != 1  -- Don't delete default collections
        AND NOT EXISTS (
            SELECT
                1
            FROM
                @Collections
            WHERE
                [Id] = CU.[CollectionId]
        )
END
GO

CREATE OR ALTER PROCEDURE [dbo].[Group_UpdateWithCollections]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @Name NVARCHAR(100),
    @ExternalId NVARCHAR(300),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @Collections AS [dbo].[CollectionAccessSelectionType] READONLY
AS
BEGIN
    SET NOCOUNT ON

    EXEC [dbo].[Group_Update] @Id, @OrganizationId, @Name, @ExternalId, @CreationDate, @RevisionDate

    -- Bump RevisionDate on all affected collections
    UPDATE C
    SET C.[RevisionDate] = @RevisionDate
    FROM [dbo].[Collection] C
    WHERE C.[OrganizationId] = @OrganizationId
    AND (
        C.[Id] IN (SELECT [Id] FROM @Collections) -- New/updated assignments
        OR C.[Id] IN (
            SELECT CG.[CollectionId]
            FROM [dbo].[CollectionGroup] CG
            WHERE CG.[GroupId] = @Id -- Existing assignments (includes ones being removed)
        )
    )

    ;WITH [AvailableCollectionsCTE] AS(
        SELECT
            Id
        FROM
            [dbo].[Collection]
        WHERE
            OrganizationId = @OrganizationId
    )
    MERGE
        [dbo].[CollectionGroup] AS [Target]
    USING
        @Collections AS [Source]
    ON
        [Target].[CollectionId] = [Source].[Id]
        AND [Target].[GroupId] = @Id
    WHEN NOT MATCHED BY TARGET
    AND [Source].[Id] IN (SELECT [Id] FROM [AvailableCollectionsCTE]) THEN
        INSERT
        (
        	[CollectionId],
        	[GroupId],
        	[ReadOnly],
        	[HidePasswords],
            [Manage]
    	)
        VALUES
        (
            [Source].[Id],
            @Id,
            [Source].[ReadOnly],
            [Source].[HidePasswords],
            [Source].[Manage]
        )
    WHEN MATCHED AND (
        [Target].[ReadOnly] != [Source].[ReadOnly]
        OR [Target].[HidePasswords] != [Source].[HidePasswords]
        OR [Target].[Manage] != [Source].[Manage]
    ) THEN
        UPDATE SET [Target].[ReadOnly] = [Source].[ReadOnly],
                   [Target].[HidePasswords] = [Source].[HidePasswords],
                   [Target].[Manage] = [Source].[Manage]
    WHEN NOT MATCHED BY SOURCE
    AND [Target].[GroupId] = @Id THEN
        DELETE
    ;

    EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationId] @OrganizationId
END
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationUser_CreateWithCollections]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER,
    @Email NVARCHAR(256),
    @Key VARCHAR(MAX),
    @Status SMALLINT,
    @Type TINYINT,
    @ExternalId NVARCHAR(300),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @Permissions NVARCHAR(MAX),
    @ResetPasswordKey VARCHAR(MAX),
    @Collections AS [dbo].[CollectionAccessSelectionType] READONLY,
    @AccessSecretsManager BIT = 0
AS
BEGIN
    SET NOCOUNT ON

    EXEC [dbo].[OrganizationUser_Create] @Id, @OrganizationId, @UserId, @Email, @Key, @Status, @Type, @ExternalId, @CreationDate, @RevisionDate, @Permissions, @ResetPasswordKey, @AccessSecretsManager

    ;WITH [AvailableCollectionsCTE] AS(
        SELECT
            [Id]
        FROM
            [dbo].[Collection]
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
        [Id],
        @Id,
        [ReadOnly],
        [HidePasswords],
        [Manage]
    FROM
        @Collections
    WHERE
        [Id] IN (SELECT [Id] FROM [AvailableCollectionsCTE])

    -- Bump RevisionDate on all affected collections
    UPDATE C
    SET C.[RevisionDate] = @RevisionDate
    FROM [dbo].[Collection] C
    WHERE C.[OrganizationId] = @OrganizationId
    AND C.[Id] IN (SELECT [Id] FROM @Collections) -- New assignments
END
GO

CREATE OR ALTER PROCEDURE [dbo].[Group_CreateWithCollections]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @Name NVARCHAR(100),
    @ExternalId NVARCHAR(300),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @Collections AS [dbo].[CollectionAccessSelectionType] READONLY
AS
BEGIN
    SET NOCOUNT ON

    EXEC [dbo].[Group_Create] @Id, @OrganizationId, @Name, @ExternalId, @CreationDate, @RevisionDate

    ;WITH [AvailableCollectionsCTE] AS(
        SELECT
            [Id]
        FROM
            [dbo].[Collection]
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
        [Id],
        @Id,
        [ReadOnly],
        [HidePasswords],
        [Manage]
    FROM
        @Collections
    WHERE
        [Id] IN (SELECT [Id] FROM [AvailableCollectionsCTE])

    -- Bump RevisionDate on all affected collections
    UPDATE C
    SET C.[RevisionDate] = @RevisionDate
    FROM [dbo].[Collection] C
    WHERE C.[OrganizationId] = @OrganizationId
    AND C.[Id] IN (SELECT [Id] FROM @Collections) -- New assignments

    EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationId] @OrganizationId
END
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationUser_CreateManyWithCollectionsAndGroups]
    @organizationUserData NVARCHAR(MAX),
    @collectionData NVARCHAR(MAX),
    @groupData NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[OrganizationUser]
    (
        [Id],
        [OrganizationId],
        [UserId],
        [Email],
        [Key],
        [Status],
        [Type],
        [ExternalId],
        [CreationDate],
        [RevisionDate],
        [Permissions],
        [ResetPasswordKey],
        [AccessSecretsManager]
    )
    SELECT
        OUI.[Id],
        OUI.[OrganizationId],
        OUI.[UserId],
        OUI.[Email],
        OUI.[Key],
        OUI.[Status],
        OUI.[Type],
        OUI.[ExternalId],
        OUI.[CreationDate],
        OUI.[RevisionDate],
        OUI.[Permissions],
        OUI.[ResetPasswordKey],
        OUI.[AccessSecretsManager]
    FROM
        OPENJSON(@organizationUserData)
                 WITH (
                     [Id] UNIQUEIDENTIFIER '$.Id',
                     [OrganizationId] UNIQUEIDENTIFIER '$.OrganizationId',
                     [UserId] UNIQUEIDENTIFIER '$.UserId',
                     [Email] NVARCHAR(256) '$.Email',
                     [Key] VARCHAR(MAX) '$.Key',
                     [Status] SMALLINT '$.Status',
                     [Type] TINYINT '$.Type',
                     [ExternalId] NVARCHAR(300) '$.ExternalId',
                     [CreationDate] DATETIME2(7) '$.CreationDate',
                     [RevisionDate] DATETIME2(7) '$.RevisionDate',
                     [Permissions] NVARCHAR (MAX) '$.Permissions',
                     [ResetPasswordKey] VARCHAR (MAX) '$.ResetPasswordKey',
                     [AccessSecretsManager] BIT '$.AccessSecretsManager'
                     ) OUI

    INSERT INTO [dbo].[GroupUser]
    (
        [OrganizationUserId],
        [GroupId]
    )
    SELECT
        OUG.OrganizationUserId,
        OUG.GroupId
    FROM
        OPENJSON(@groupData)
            WITH(
                [OrganizationUserId] UNIQUEIDENTIFIER '$.OrganizationUserId',
                [GroupId] UNIQUEIDENTIFIER '$.GroupId'
            ) OUG

    INSERT INTO [dbo].[CollectionUser]
    (
        [CollectionId],
        [OrganizationUserId],
        [ReadOnly],
        [HidePasswords],
        [Manage]
    )
    SELECT
        OUC.[CollectionId],
        OUC.[OrganizationUserId],
        OUC.[ReadOnly],
        OUC.[HidePasswords],
        OUC.[Manage]
    FROM
        OPENJSON(@collectionData)
            WITH(
                [CollectionId] UNIQUEIDENTIFIER '$.CollectionId',
                [OrganizationUserId] UNIQUEIDENTIFIER '$.OrganizationUserId',
                [ReadOnly] BIT '$.ReadOnly',
                [HidePasswords] BIT '$.HidePasswords',
                [Manage] BIT '$.Manage'
            ) OUC

    -- Bump RevisionDate on all affected collections
    UPDATE C
    SET C.[RevisionDate] = GETUTCDATE()
    FROM [dbo].[Collection] C
    WHERE C.[Id] IN (
        SELECT OUC.[CollectionId]
        FROM OPENJSON(@collectionData)
        WITH ([CollectionId] UNIQUEIDENTIFIER '$.CollectionId') OUC
    )
END
GO
