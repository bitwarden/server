IF COL_LENGTH('[dbo].[OrganizationUser]', 'AccessPam') IS NULL
BEGIN
    ALTER TABLE
        [dbo].[OrganizationUser]
    ADD
        [AccessPam] BIT NOT NULL CONSTRAINT [DF_OrganizationUser_Pam] DEFAULT (0)
END
GO

IF OBJECT_ID('[dbo].[OrganizationUserView]') IS NOT NULL
BEGIN
    EXECUTE sp_refreshsqlmodule N'[dbo].[OrganizationUserView]';
END
GO

CREATE OR ALTER VIEW [dbo].[OrganizationUserUserDetailsView]
AS
SELECT
    OU.[Id],
    OU.[UserId],
    OU.[OrganizationId],
    U.[Name],
    ISNULL(U.[Email], OU.[Email]) Email,
    U.[AvatarColor],
    U.[TwoFactorProviders],
    U.[Premium],
    OU.[Status],
    OU.[Type],
    OU.[AccessSecretsManager],
    OU.[ExternalId],
    SU.[ExternalId] SsoExternalId,
    OU.[Permissions],
    OU.[ResetPasswordKey],
    U.[UsesKeyConnector],
    CASE WHEN U.[MasterPassword] IS NOT NULL THEN 1 ELSE 0 END AS HasMasterPassword,
    OU.[RevocationReason],
    OU.[CreationDate],
    OU.[AccessPam]
FROM
    [dbo].[OrganizationUser] OU
LEFT JOIN
    [dbo].[User] U ON U.[Id] = OU.[UserId]
LEFT JOIN
    [dbo].[SsoUser] SU ON SU.[UserId] = OU.[UserId] AND SU.[OrganizationId] = OU.[OrganizationId]
GO

CREATE OR ALTER VIEW [dbo].[OrganizationUserOrganizationDetailsView]
AS
SELECT
    OU.[UserId],
    OU.[OrganizationId],
    OU.[Id] OrganizationUserId,
    O.[Name],
    O.[Enabled],
    O.[PlanType],
    O.[UsePolicies],
    O.[UseSso],
    O.[UseKeyConnector],
    O.[UseScim],
    O.[UseGroups],
    O.[UseDirectory],
    O.[UseEvents],
    O.[UseTotp],
    O.[Use2fa],
    O.[UseApi],
    O.[UseResetPassword],
    O.[SelfHost],
    O.[UsersGetPremium],
    O.[UseCustomPermissions],
    O.[UseSecretsManager],
    O.[Seats],
    O.[MaxCollections],
    COALESCE(O.[MaxStorageGbIncreased], O.[MaxStorageGb]) AS [MaxStorageGb],
    O.[Identifier],
    OU.[Key],
    OU.[ResetPasswordKey],
    O.[PublicKey],
    O.[PrivateKey],
    OU.[Status],
    OU.[Type],
    SU.[ExternalId] SsoExternalId,
    OU.[Permissions],
    PO.[ProviderId],
    P.[Name] ProviderName,
    P.[Type] ProviderType,
    SS.[Enabled] SsoEnabled,
    SS.[Data] SsoConfig,
    OS.[FriendlyName] FamilySponsorshipFriendlyName,
    OS.[LastSyncDate] FamilySponsorshipLastSyncDate,
    OS.[ToDelete] FamilySponsorshipToDelete,
    OS.[ValidUntil] FamilySponsorshipValidUntil,
    OU.[AccessSecretsManager],
    O.[UsePasswordManager],
    O.[SmSeats],
    O.[SmServiceAccounts],
    O.[LimitCollectionCreation],
    O.[LimitCollectionDeletion],
    O.[AllowAdminAccessToAllCollectionItems],
    O.[UseRiskInsights],
    O.[LimitItemDeletion],
    O.[UseAdminSponsoredFamilies],
    O.[UseOrganizationDomains],
    OS.[IsAdminInitiated],
    O.[UseAutomaticUserConfirmation],
    O.[UsePhishingBlocker],
    O.[UseDisableSmAdsForUsers],
    O.[UseMyItems],
    O.[UseInviteLinks],
    O.[UsePam],
    OU.[RevocationReason],
    OU.[AccessPam]
FROM
    [dbo].[OrganizationUser] OU
LEFT JOIN
    [dbo].[Organization] O ON O.[Id] = OU.[OrganizationId]
LEFT JOIN
    [dbo].[SsoUser] SU ON SU.[UserId] = OU.[UserId] AND SU.[OrganizationId] = OU.[OrganizationId]
LEFT JOIN
    [dbo].[ProviderOrganization] PO ON PO.[OrganizationId] = O.[Id]
LEFT JOIN
    [dbo].[Provider] P ON P.[Id] = PO.[ProviderId]
LEFT JOIN
    [dbo].[SsoConfig] SS ON SS.[OrganizationId] = OU.[OrganizationId]
LEFT JOIN
    [dbo].[OrganizationSponsorship] OS ON OS.[SponsoringOrganizationUserID] = OU.[Id]
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
    @AccessPam BIT = 0
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
        [AccessPam]
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
        @AccessPam
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
    @AccessPam BIT = 0
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
        [AccessPam] = @AccessPam
    WHERE
        [Id] = @Id

    EXEC [dbo].[User_BumpAccountRevisionDate] @UserId
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
        [AccessPam]
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
        ISNULL(OUI.[AccessPam], 0)
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
            [AccessPam] BIT '$.AccessPam'
        ) OUI
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
        [AccessPam] BIT
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
        [AccessPam]
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
        [AccessPam] BIT '$.AccessPam'
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
        [AccessPam] = ISNULL(OUI.[AccessPam], 0)
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
    @AccessPam BIT = 0
AS
BEGIN
    SET NOCOUNT ON

    EXEC [dbo].[OrganizationUser_Create] @Id, @OrganizationId, @UserId, @Email, @Key, @Status, @Type, @ExternalId, @CreationDate, @RevisionDate, @Permissions, @ResetPasswordKey, @AccessSecretsManager, @RevocationReason, @StatusNew, @AccessPam

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
    @AccessPam BIT = 0
AS
BEGIN
    SET NOCOUNT ON

    EXEC [dbo].[OrganizationUser_Update] @Id, @OrganizationId, @UserId, @Email, @Key, @Status, @Type, @ExternalId, @CreationDate, @RevisionDate, @Permissions, @ResetPasswordKey, @AccessSecretsManager, @RevocationReason, @StatusNew, @AccessPam

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
        [AccessPam]
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
        ISNULL(OUI.[AccessPam], 0)
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
                     [AccessPam] BIT '$.AccessPam'
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

CREATE OR ALTER PROCEDURE [dbo].[OrganizationUser_ReadOccupiedPamSeatCountByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        COUNT(1)
    FROM
        [dbo].[OrganizationUserView]
    WHERE
        OrganizationId = @OrganizationId
        AND Status IN (0, 1, 2) -- Invited, Accepted, Confirmed
        AND AccessPam = 1
END
GO

-- Refresh the stored procedures that SELECT * off the OrganizationUser detail
-- views so their cached result-set metadata reflects the new [AccessPam] column.
IF OBJECT_ID('[dbo].[OrganizationUserUserDetails_ReadById]') IS NOT NULL
BEGIN
    EXECUTE sp_refreshsqlmodule N'[dbo].[OrganizationUserUserDetails_ReadById]';
END
GO

IF OBJECT_ID('[dbo].[OrganizationUserUserDetails_ReadByOrganizationId]') IS NOT NULL
BEGIN
    EXECUTE sp_refreshsqlmodule N'[dbo].[OrganizationUserUserDetails_ReadByOrganizationId]';
END
GO

IF OBJECT_ID('[dbo].[OrganizationUserUserDetails_ReadByOrganizationId_V2]') IS NOT NULL
BEGIN
    EXECUTE sp_refreshsqlmodule N'[dbo].[OrganizationUserUserDetails_ReadByOrganizationId_V2]';
END
GO

IF OBJECT_ID('[dbo].[OrganizationUserUserDetails_ReadByOrganizationIdUserId]') IS NOT NULL
BEGIN
    EXECUTE sp_refreshsqlmodule N'[dbo].[OrganizationUserUserDetails_ReadByOrganizationIdUserId]';
END
GO

IF OBJECT_ID('[dbo].[OrganizationUser_ReadByMinimumRole]') IS NOT NULL
BEGIN
    EXECUTE sp_refreshsqlmodule N'[dbo].[OrganizationUser_ReadByMinimumRole]';
END
GO

IF OBJECT_ID('[dbo].[OrganizationUser_ReadManyDetailsByRole]') IS NOT NULL
BEGIN
    EXECUTE sp_refreshsqlmodule N'[dbo].[OrganizationUser_ReadManyDetailsByRole]';
END
GO

IF OBJECT_ID('[dbo].[OrganizationUserOrganizationDetails_ReadAcceptedConfirmedByUserId]') IS NOT NULL
BEGIN
    EXECUTE sp_refreshsqlmodule N'[dbo].[OrganizationUserOrganizationDetails_ReadAcceptedConfirmedByUserId]';
END
GO

IF OBJECT_ID('[dbo].[OrganizationUserOrganizationDetails_ReadByUserIdStatus]') IS NOT NULL
BEGIN
    EXECUTE sp_refreshsqlmodule N'[dbo].[OrganizationUserOrganizationDetails_ReadByUserIdStatus]';
END
GO

IF OBJECT_ID('[dbo].[OrganizationUserOrganizationDetails_ReadByUserIdStatusOrganizationId]') IS NOT NULL
BEGIN
    EXECUTE sp_refreshsqlmodule N'[dbo].[OrganizationUserOrganizationDetails_ReadByUserIdStatusOrganizationId]';
END
GO

-- Refresh the remaining views that reference [dbo].[OrganizationUser] so their
-- cached metadata reflects the new [AccessPam] column.
IF OBJECT_ID('[dbo].[ProviderOrganizationOrganizationDetailsView]') IS NOT NULL
BEGIN
    EXECUTE sp_refreshsqlmodule N'[dbo].[ProviderOrganizationOrganizationDetailsView]';
END
GO

IF OBJECT_ID('[dbo].[UserPremiumAccessView]') IS NOT NULL
BEGIN
    EXECUTE sp_refreshsqlmodule N'[dbo].[UserPremiumAccessView]';
END
GO
