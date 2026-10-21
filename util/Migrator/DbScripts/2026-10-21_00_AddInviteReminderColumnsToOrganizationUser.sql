-- Add the invite reminder columns to OrganizationUser
IF COL_LENGTH('[dbo].[OrganizationUser]', 'InviteReminderDueDate') IS NULL
BEGIN
    ALTER TABLE [dbo].[OrganizationUser]
        ADD [InviteReminderDueDate] DATETIME2(7) NULL;
END
GO

IF COL_LENGTH('[dbo].[OrganizationUser]', 'InviteRemindersSentCount') IS NULL
BEGIN
    ALTER TABLE [dbo].[OrganizationUser]
        ADD [InviteRemindersSentCount] TINYINT NULL;
END
GO

-- Refresh the core OrganizationUser view (SELECT * over the table) and its dependents
EXEC sp_refreshview N'[dbo].[OrganizationUserView]';
EXEC sp_refreshview N'[dbo].[OrganizationUserOrganizationDetailsView]';
EXEC sp_refreshview N'[dbo].[OrganizationUserUserDetailsView]';
EXEC sp_refreshview N'[dbo].[ProviderOrganizationOrganizationDetailsView]';
EXEC sp_refreshview N'[dbo].[UserPremiumAccessView]';
GO

-- Index for the invite reminder claim
IF NOT EXISTS (
    SELECT
        *
    FROM
        sys.indexes
    WHERE
        name = 'IX_OrganizationUser_InviteReminders_Filtered'
        AND object_id = OBJECT_ID('[dbo].[OrganizationUser]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_OrganizationUser_InviteReminders_Filtered]
        ON [dbo].[OrganizationUser]([InviteReminderDueDate] ASC, [InviteRemindersSentCount] ASC)
        INCLUDE ([OrganizationId], [Email])
        WHERE [Status] = 0 -- 0 = Invited
            AND [InviteReminderDueDate] IS NOT NULL
            AND [InviteRemindersSentCount] < 3;
END
GO

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
    @StatusNew SMALLINT = NULL,
    @AccessPam BIT = 0,
    @V2UpgradeToken VARCHAR(MAX) = NULL,
    @InviteReminderDueDate DATETIME2(7) = NULL,
    @InviteRemindersSentCount TINYINT = NULL
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
        [StatusNew],
        [AccessPam],
        [V2UpgradeToken],
        [InviteReminderDueDate],
        [InviteRemindersSentCount]
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
        @StatusNew,
        @AccessPam,
        @V2UpgradeToken,
        @InviteReminderDueDate,
        @InviteRemindersSentCount
    )
END
GO

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
    @StatusNew SMALLINT = NULL,
    @AccessPam BIT = 0,
    @V2UpgradeToken VARCHAR(MAX) = NULL,
    @InviteReminderDueDate DATETIME2(7) = NULL,
    @InviteRemindersSentCount TINYINT = NULL
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
        [StatusNew] = @StatusNew,
        [AccessPam] = @AccessPam,
        [V2UpgradeToken] = @V2UpgradeToken,
        [InviteReminderDueDate] = @InviteReminderDueDate,
        [InviteRemindersSentCount] = @InviteRemindersSentCount
    WHERE
        [Id] = @Id

    EXEC [dbo].[User_BumpAccountRevisionDate] @UserId
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
    @AccessSecretsManager BIT = 0,
    @RevocationReason TINYINT = NULL,
    @StatusNew SMALLINT = NULL,
    @AccessPam BIT = 0,
    @V2UpgradeToken VARCHAR(MAX) = NULL,
    @InviteReminderDueDate DATETIME2(7) = NULL,
    @InviteRemindersSentCount TINYINT = NULL
AS
BEGIN
    SET NOCOUNT ON

    EXEC [dbo].[OrganizationUser_Create] @Id, @OrganizationId, @UserId, @Email, @Key, @Status, @Type, @ExternalId, @CreationDate, @RevisionDate, @Permissions, @ResetPasswordKey, @AccessSecretsManager, @RevocationReason, @StatusNew, @AccessPam, @V2UpgradeToken, @InviteReminderDueDate, @InviteRemindersSentCount

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
    @StatusNew SMALLINT = NULL,
    @AccessPam BIT = 0,
    @V2UpgradeToken VARCHAR(MAX) = NULL,
    @InviteReminderDueDate DATETIME2(7) = NULL,
    @InviteRemindersSentCount TINYINT = NULL
AS
BEGIN
    SET NOCOUNT ON

    EXEC [dbo].[OrganizationUser_Update] @Id, @OrganizationId, @UserId, @Email, @Key, @Status, @Type, @ExternalId, @CreationDate, @RevisionDate, @Permissions, @ResetPasswordKey, @AccessSecretsManager, @RevocationReason, @StatusNew, @AccessPam, @V2UpgradeToken, @InviteReminderDueDate, @InviteRemindersSentCount

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
        [StatusNew] SMALLINT NULL,
        [AccessPam] BIT,
        [V2UpgradeToken] VARCHAR(MAX) NULL,
        [InviteReminderDueDate] DATETIME2(7) NULL,
        [InviteRemindersSentCount] TINYINT NULL
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
        [StatusNew],
        [AccessPam],
        [V2UpgradeToken],
        [InviteReminderDueDate],
        [InviteRemindersSentCount]
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
        [StatusNew] SMALLINT '$.StatusNew',
        [AccessPam] BIT '$.AccessPam',
        [V2UpgradeToken] VARCHAR(MAX) '$.V2UpgradeToken',
        [InviteReminderDueDate] DATETIME2(7) '$.InviteReminderDueDate',
        [InviteRemindersSentCount] TINYINT '$.InviteRemindersSentCount'
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
        [StatusNew] = OUI.[StatusNew],
        [AccessPam] = ISNULL(OUI.[AccessPam], 0),
        [V2UpgradeToken] = OUI.[V2UpgradeToken],
        [InviteReminderDueDate] = OUI.[InviteReminderDueDate],
        [InviteRemindersSentCount] = OUI.[InviteRemindersSentCount]
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
        [StatusNew],
        [AccessPam],
        [InviteReminderDueDate],
        [InviteRemindersSentCount]
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
        OUI.[StatusNew],
        ISNULL(OUI.[AccessPam], 0),
        OUI.[InviteReminderDueDate],
        OUI.[InviteRemindersSentCount]
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
            [StatusNew] SMALLINT '$.StatusNew',
            [AccessPam] BIT '$.AccessPam',
            [InviteReminderDueDate] DATETIME2(7) '$.InviteReminderDueDate',
            [InviteRemindersSentCount] TINYINT '$.InviteRemindersSentCount'
        ) OUI
END
GO

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
        [StatusNew],
        [AccessPam],
        [InviteReminderDueDate],
        [InviteRemindersSentCount]
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
        OUI.[StatusNew],
        ISNULL(OUI.[AccessPam], 0),
        OUI.[InviteReminderDueDate],
        OUI.[InviteRemindersSentCount]
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
                     [StatusNew] SMALLINT '$.StatusNew',
                     [AccessPam] BIT '$.AccessPam',
                     [InviteReminderDueDate] DATETIME2(7) '$.InviteReminderDueDate',
                     [InviteRemindersSentCount] TINYINT '$.InviteRemindersSentCount'
                     ) OUI

    -- Only groups in the user's organization may be attached
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
    INNER JOIN
        [dbo].[OrganizationUser] OU ON OU.[Id] = OUG.[OrganizationUserId]
    INNER JOIN
        [dbo].[Group] G ON G.[Id] = OUG.[GroupId] AND G.[OrganizationId] = OU.[OrganizationId]

    -- Only collections in the user's organization may be attached; this also scopes the RevisionDate bump below
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
    INNER JOIN
        [dbo].[OrganizationUser] OU ON OU.[Id] = OUC.[OrganizationUserId]
    INNER JOIN
        [dbo].[Collection] C ON C.[Id] = OUC.[CollectionId] AND C.[OrganizationId] = OU.[OrganizationId]

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

CREATE OR ALTER PROCEDURE [dbo].[OrganizationUser_UpdateManyClaimInviteReminderSequences]
    @Now DATETIME2(7),
    @OldestDueDate DATETIME2(7),
    @NextDueDate DATETIME2(7),
    @BatchSize INT
AS
BEGIN
    SET NOCOUNT ON

    -- Status and the reminder limit are literals, not parameters, so the query matches
    -- the filter of IX_OrganizationUser_InviteReminders_Filtered
    ;WITH [Due] AS (
        SELECT TOP (@BatchSize)
            OU.[Id],
            OU.[OrganizationId],
            OU.[Email],
            OU.[InviteReminderDueDate],
            OU.[InviteRemindersSentCount]
        FROM
            [dbo].[OrganizationUser] OU WITH (UPDLOCK, READPAST)
        INNER JOIN
            [dbo].[Organization] O ON O.[Id] = OU.[OrganizationId]
        WHERE
            OU.[Status] = 0 -- 0 = Invited
            AND OU.[Email] IS NOT NULL
            AND OU.[InviteReminderDueDate] IS NOT NULL
            AND OU.[InviteReminderDueDate] <= @Now
            AND OU.[InviteReminderDueDate] >= @OldestDueDate
            AND OU.[InviteRemindersSentCount] < 3
            AND O.[Enabled] = 1
        ORDER BY
            OU.[InviteReminderDueDate] ASC
    )
    UPDATE
        [Due]
    SET
        [InviteRemindersSentCount] = [InviteRemindersSentCount] + 1,
        [InviteReminderDueDate] = @NextDueDate
    OUTPUT
        inserted.[Id],
        inserted.[OrganizationId],
        inserted.[Email],
        inserted.[InviteRemindersSentCount]
END
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationUser_UpdateManyStartInviteReminderSequences]
    @OrganizationUserIds AS [dbo].[GuidIdArray] READONLY,
    @DueDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    -- Does not bump the account revision date: the reminder columns are not synced to clients
    UPDATE
        OU
    SET
        OU.[InviteRemindersSentCount] = 0,
        -- Keep a later stored due date
        OU.[InviteReminderDueDate] = CASE
            WHEN OU.[InviteReminderDueDate] > @DueDate
            THEN OU.[InviteReminderDueDate]
            ELSE @DueDate
        END
    FROM
        [dbo].[OrganizationUser] OU
    INNER JOIN
        @OrganizationUserIds OUI ON OUI.[Id] = OU.[Id]
END
GO
