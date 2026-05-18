-- Add StatusNew column to OrganizationUser
IF COL_LENGTH('[dbo].[OrganizationUser]', 'StatusNew') IS NULL
BEGIN
    ALTER TABLE [dbo].[OrganizationUser]
        ADD [StatusNew] SMALLINT NULL;
END
GO

-- Refresh the core OrganizationUser view (SELECT * over the table)
EXEC sp_refreshview N'[dbo].[OrganizationUserView]';
GO

-- Update OrganizationUser_Create
CREATE OR ALTER PROCEDURE [dbo].[OrganizationUser_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
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
    @AccessSecretsManager BIT = 0,
    @RevocationReason TINYINT = NULL,
    @StatusNew SMALLINT = NULL
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
        [AccessSecretsManager],
        [RevocationReason],
        [StatusNew]
    )
    VALUES
    (
        @Id,
        @OrganizationId,
        @UserId,
        @Email,
        @Key,
        @Status,
        @Type,
        @ExternalId,
        @CreationDate,
        @RevisionDate,
        @Permissions,
        @ResetPasswordKey,
        @AccessSecretsManager,
        @RevocationReason,
        @StatusNew
    )
END
GO

-- Update OrganizationUser_Update
CREATE OR ALTER PROCEDURE [dbo].[OrganizationUser_Update]
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
    @AccessSecretsManager BIT = 0,
    @RevocationReason TINYINT = NULL,
    @StatusNew SMALLINT = NULL
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[OrganizationUser]
    SET
        [OrganizationId] = @OrganizationId,
        [UserId] = @UserId,
        [Email] = @Email,
        [Key] = @Key,
        [Status] = @Status,
        [Type] = @Type,
        [ExternalId] = @ExternalId,
        [CreationDate] = @CreationDate,
        [RevisionDate] = @RevisionDate,
        [Permissions] = @Permissions,
        [ResetPasswordKey] = @ResetPasswordKey,
        [AccessSecretsManager] = @AccessSecretsManager,
        [RevocationReason] = @RevocationReason,
        [StatusNew] = @StatusNew
    WHERE
        [Id] = @Id

    EXEC [dbo].[User_BumpAccountRevisionDate] @UserId
END
GO

-- Update OrganizationUser_CreateWithCollections
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
    @AccessSecretsManager BIT = 0,
    @RevocationReason TINYINT = NULL,
    @StatusNew SMALLINT = NULL
AS
BEGIN
    SET NOCOUNT ON

    EXEC [dbo].[OrganizationUser_Create] @Id, @OrganizationId, @UserId, @Email, @Key, @Status, @Type, @ExternalId, @CreationDate, @RevisionDate, @Permissions, @ResetPasswordKey, @AccessSecretsManager, @RevocationReason, @StatusNew

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
    UPDATE
        C
    SET
        C.[RevisionDate] = @RevisionDate
    FROM
        [dbo].[Collection] C
    WHERE
        C.[OrganizationId] = @OrganizationId
        AND C.[Id] IN (SELECT [Id] FROM @Collections)
END
GO

-- Update OrganizationUser_UpdateWithCollections
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
    @AccessSecretsManager BIT = 0,
    @RevocationReason TINYINT = NULL,
    @StatusNew SMALLINT = NULL
AS
BEGIN
    SET NOCOUNT ON

    EXEC [dbo].[OrganizationUser_Update] @Id, @OrganizationId, @UserId, @Email, @Key, @Status, @Type, @ExternalId, @CreationDate, @RevisionDate, @Permissions, @ResetPasswordKey, @AccessSecretsManager, @RevocationReason, @StatusNew

    -- Bump RevisionDate on all affected collections
    ;WITH [AffectedCollectionsCTE] AS (
        SELECT
            [Id]
        FROM
            @Collections

        UNION

        SELECT
            CU.[CollectionId]
        FROM
            [dbo].[CollectionUser] CU
        WHERE
            CU.[OrganizationUserId] = @Id
    )
    UPDATE
        C
    SET
        C.[RevisionDate] = @RevisionDate
    FROM
        [dbo].[Collection] C
    WHERE
        C.[OrganizationId] = @OrganizationId
        AND C.[Id] IN (SELECT [Id] FROM [AffectedCollectionsCTE])

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

-- Update OrganizationUser_CreateMany
CREATE OR ALTER PROCEDURE [dbo].[OrganizationUser_CreateMany]
    @jsonData NVARCHAR(MAX)
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
        [AccessSecretsManager],
        [RevocationReason],
        [StatusNew]
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
        OUI.[AccessSecretsManager],
        OUI.[RevocationReason],
        OUI.[StatusNew]
    FROM
        OPENJSON(@jsonData)
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
            [AccessSecretsManager] BIT '$.AccessSecretsManager',
            [RevocationReason] TINYINT '$.RevocationReason',
            [StatusNew] SMALLINT '$.StatusNew'
        ) OUI
END
GO

-- Update OrganizationUser_UpdateMany
CREATE OR ALTER PROCEDURE [dbo].[OrganizationUser_UpdateMany]
    @jsonData NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @UserIds [dbo].[GuidIdArray]

    -- Parse the JSON string
    DECLARE @OrganizationUserInput AS TABLE (
        [Id] UNIQUEIDENTIFIER,
        [OrganizationId] UNIQUEIDENTIFIER,
        [UserId] UNIQUEIDENTIFIER,
        [Email] NVARCHAR(256),
        [Key] VARCHAR(MAX),
        [Status] SMALLINT,
        [Type] TINYINT,
        [ExternalId] NVARCHAR(300),
        [CreationDate] DATETIME2(7),
        [RevisionDate] DATETIME2(7),
        [Permissions] NVARCHAR(MAX),
        [ResetPasswordKey] VARCHAR(MAX),
        [AccessSecretsManager] BIT,
        [RevocationReason] TINYINT NULL,
        [StatusNew] SMALLINT NULL
    )

    INSERT INTO @OrganizationUserInput
    SELECT
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
        [AccessSecretsManager],
        [RevocationReason],
        [StatusNew]
    FROM OPENJSON(@jsonData)
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
        [AccessSecretsManager] BIT '$.AccessSecretsManager',
        [RevocationReason] TINYINT '$.RevocationReason',
        [StatusNew] SMALLINT '$.StatusNew'
    )

    -- Perform the update
    UPDATE
        OU
    SET
        [OrganizationId] = OUI.[OrganizationId],
        [UserId] = OUI.[UserId],
        [Email] = OUI.[Email],
        [Key] = OUI.[Key],
        [Status] = OUI.[Status],
        [Type] = OUI.[Type],
        [ExternalId] = OUI.[ExternalId],
        [CreationDate] = OUI.[CreationDate],
        [RevisionDate] = OUI.[RevisionDate],
        [Permissions] = OUI.[Permissions],
        [ResetPasswordKey] = OUI.[ResetPasswordKey],
        [AccessSecretsManager] = OUI.[AccessSecretsManager],
        [RevocationReason] = OUI.[RevocationReason],
        [StatusNew] = OUI.[StatusNew]
    FROM
        [dbo].[OrganizationUser] OU
    INNER JOIN
        @OrganizationUserInput OUI ON OU.Id = OUI.Id

    -- Bump account revision dates
    INSERT INTO @UserIds
    SELECT [UserId]
    FROM @OrganizationUserInput
    WHERE [UserId] IS NOT NULL

    EXEC [dbo].[User_BumpManyAccountRevisionDates] @UserIds
END
GO

-- Update OrganizationUser_CreateManyWithCollectionsAndGroups
CREATE OR ALTER PROCEDURE [dbo].[OrganizationUser_CreateManyWithCollectionsAndGroups]
    @organizationUserData NVARCHAR(MAX),
    @collectionData NVARCHAR(MAX),
    @groupData NVARCHAR(MAX),
    @RevisionDate DATETIME2(7) = NULL
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
        [AccessSecretsManager],
        [RevocationReason],
        [StatusNew]
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
        OUI.[AccessSecretsManager],
        OUI.[RevocationReason],
        OUI.[StatusNew]
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
                     [AccessSecretsManager] BIT '$.AccessSecretsManager',
                     [RevocationReason] TINYINT '$.RevocationReason',
                     [StatusNew] SMALLINT '$.StatusNew'
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

    SELECT
        OUC.[CollectionId],
        OUC.[OrganizationUserId],
        OUC.[ReadOnly],
        OUC.[HidePasswords],
        OUC.[Manage]
    INTO #CollectionUserData
    FROM
        OPENJSON(@collectionData)
            WITH(
                [CollectionId] UNIQUEIDENTIFIER '$.CollectionId',
                [OrganizationUserId] UNIQUEIDENTIFIER '$.OrganizationUserId',
                [ReadOnly] BIT '$.ReadOnly',
                [HidePasswords] BIT '$.HidePasswords',
                [Manage] BIT '$.Manage'
            ) OUC

    INSERT INTO [dbo].[CollectionUser]
    (
        [CollectionId],
        [OrganizationUserId],
        [ReadOnly],
        [HidePasswords],
        [Manage]
    )
    SELECT
        [CollectionId],
        [OrganizationUserId],
        [ReadOnly],
        [HidePasswords],
        [Manage]
    FROM #CollectionUserData

    -- Bump RevisionDate on all affected collections
    IF @RevisionDate IS NOT NULL
    BEGIN
        UPDATE
            C
        SET
            C.[RevisionDate] = @RevisionDate
        FROM
            [dbo].[Collection] C
        INNER JOIN
            #CollectionUserData CUD ON CUD.[CollectionId] = C.[Id]
    END
END
GO
