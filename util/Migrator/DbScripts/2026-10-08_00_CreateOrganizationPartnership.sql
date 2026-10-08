IF OBJECT_ID('[dbo].[OrganizationPartnership]') IS NULL
BEGIN
    CREATE TABLE [dbo].[OrganizationPartnership]
    (
        [Id]                           UNIQUEIDENTIFIER NOT NULL,
        [OrganizationId]               UNIQUEIDENTIFIER NOT NULL,
        [Name]                         NVARCHAR(50)     NOT NULL,
        [Status]                       TINYINT          NOT NULL,
        [SponsoredPlanType]            TINYINT          NOT NULL,
        [BindingMode]                  TINYINT          NOT NULL,
        [IdentityBindingConfiguration] NVARCHAR(MAX)    NULL,
        [RegisteredReturnOrigins]      NVARCHAR(MAX)    NOT NULL,
        [CreationDate]                 DATETIME2(7)     NOT NULL,
        [RevisionDate]                 DATETIME2(7)     NOT NULL,
        CONSTRAINT [PK_OrganizationPartnership] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_OrganizationPartnership_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id])
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'IX_OrganizationPartnership_OrganizationId' AND object_id = OBJECT_ID('[dbo].[OrganizationPartnership]'))
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [IX_OrganizationPartnership_OrganizationId]
        ON [dbo].[OrganizationPartnership]([OrganizationId] ASC);
END
GO

IF OBJECT_ID('[dbo].[OrganizationPartnershipEntitlement]') IS NULL
BEGIN
    CREATE TABLE [dbo].[OrganizationPartnershipEntitlement]
    (
        [Id]                         UNIQUEIDENTIFIER NOT NULL,
        [OrganizationPartnershipId]  UNIQUEIDENTIFIER NOT NULL,
        [ExternalId]                 NVARCHAR(MAX)    NOT NULL,
        [ExternalIdHash]             VARCHAR(64)      NOT NULL,
        [State]                      TINYINT          NOT NULL,
        [UserId]                     UNIQUEIDENTIFIER NULL,
        [AccountRef]                 UNIQUEIDENTIFIER NULL,
        [Metadata]                   NVARCHAR(MAX)    NULL,
        [BoundDate]                  DATETIME2(7)     NULL,
        [SuspendedDate]              DATETIME2(7)     NULL,
        [CanceledDate]               DATETIME2(7)     NULL,
        [ResumeWindowExpirationDate] DATETIME2(7)     NULL,
        [LastAppliedEffectiveDate]   DATETIME2(7)     NOT NULL,
        [CreationDate]               DATETIME2(7)     NOT NULL,
        [RevisionDate]               DATETIME2(7)     NOT NULL,
        CONSTRAINT [PK_OrganizationPartnershipEntitlement] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_OrganizationPartnershipEntitlement_OrganizationPartnership] FOREIGN KEY ([OrganizationPartnershipId]) REFERENCES [dbo].[OrganizationPartnership] ([Id]),
        CONSTRAINT [FK_OrganizationPartnershipEntitlement_User] FOREIGN KEY ([UserId]) REFERENCES [dbo].[User] ([Id])
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'IX_OrganizationPartnershipEntitlement_OrganizationPartnershipId_ExternalIdHash' AND object_id = OBJECT_ID('[dbo].[OrganizationPartnershipEntitlement]'))
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [IX_OrganizationPartnershipEntitlement_OrganizationPartnershipId_ExternalIdHash]
        ON [dbo].[OrganizationPartnershipEntitlement]([OrganizationPartnershipId] ASC, [ExternalIdHash] ASC);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'IX_OrganizationPartnershipEntitlement_State_ResumeWindowExpirationDate' AND object_id = OBJECT_ID('[dbo].[OrganizationPartnershipEntitlement]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_OrganizationPartnershipEntitlement_State_ResumeWindowExpirationDate]
        ON [dbo].[OrganizationPartnershipEntitlement]([State] ASC, [ResumeWindowExpirationDate] ASC)
        WHERE [UserId] IS NOT NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'IX_OrganizationPartnershipEntitlement_UserId' AND object_id = OBJECT_ID('[dbo].[OrganizationPartnershipEntitlement]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_OrganizationPartnershipEntitlement_UserId]
        ON [dbo].[OrganizationPartnershipEntitlement]([UserId] ASC);
END
GO

CREATE OR ALTER VIEW [dbo].[OrganizationPartnershipView]
AS
SELECT
    *
FROM
    [dbo].[OrganizationPartnership]
GO

CREATE OR ALTER VIEW [dbo].[OrganizationPartnershipEntitlementView]
AS
SELECT
    *
FROM
    [dbo].[OrganizationPartnershipEntitlement]
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationPartnership_Create]
    @Id                           UNIQUEIDENTIFIER OUTPUT,
    @OrganizationId               UNIQUEIDENTIFIER,
    @Name                         NVARCHAR(50),
    @Status                       TINYINT,
    @SponsoredPlanType            TINYINT,
    @BindingMode                  TINYINT,
    @IdentityBindingConfiguration NVARCHAR(MAX),
    @RegisteredReturnOrigins      NVARCHAR(MAX),
    @CreationDate                 DATETIME2(7),
    @RevisionDate                 DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[OrganizationPartnership]
    (
        [Id],
        [OrganizationId],
        [Name],
        [Status],
        [SponsoredPlanType],
        [BindingMode],
        [IdentityBindingConfiguration],
        [RegisteredReturnOrigins],
        [CreationDate],
        [RevisionDate]
    )
    VALUES
    (
        @Id,
        @OrganizationId,
        @Name,
        @Status,
        @SponsoredPlanType,
        @BindingMode,
        @IdentityBindingConfiguration,
        @RegisteredReturnOrigins,
        @CreationDate,
        @RevisionDate
    )
END
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationPartnership_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[OrganizationPartnershipView]
    WHERE
        [Id] = @Id
END
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationPartnership_ReadByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[OrganizationPartnershipView]
    WHERE
        [OrganizationId] = @OrganizationId
END
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationPartnership_Update]
    @Id                           UNIQUEIDENTIFIER,
    @OrganizationId               UNIQUEIDENTIFIER,
    @Name                         NVARCHAR(50),
    @Status                       TINYINT,
    @SponsoredPlanType            TINYINT,
    @BindingMode                  TINYINT,
    @IdentityBindingConfiguration NVARCHAR(MAX),
    @RegisteredReturnOrigins      NVARCHAR(MAX),
    @CreationDate                 DATETIME2(7),
    @RevisionDate                 DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[OrganizationPartnership]
    SET
        [OrganizationId] = @OrganizationId,
        [Name] = @Name,
        [Status] = @Status,
        [SponsoredPlanType] = @SponsoredPlanType,
        [BindingMode] = @BindingMode,
        [IdentityBindingConfiguration] = @IdentityBindingConfiguration,
        [RegisteredReturnOrigins] = @RegisteredReturnOrigins,
        [CreationDate] = @CreationDate,
        [RevisionDate] = @RevisionDate
    WHERE
        [Id] = @Id
END
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationPartnership_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    BEGIN TRANSACTION OrganizationPartnership_DeleteById

    DELETE
    FROM
        [dbo].[OrganizationPartnershipEntitlement]
    WHERE
        [OrganizationPartnershipId] = @Id

    DELETE
    FROM
        [dbo].[OrganizationPartnership]
    WHERE
        [Id] = @Id

    COMMIT TRANSACTION OrganizationPartnership_DeleteById
END
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationPartnershipEntitlement_Create]
    @Id                         UNIQUEIDENTIFIER OUTPUT,
    @OrganizationPartnershipId  UNIQUEIDENTIFIER,
    @ExternalId                 NVARCHAR(MAX),
    @ExternalIdHash             VARCHAR(64),
    @State                      TINYINT,
    @UserId                     UNIQUEIDENTIFIER,
    @AccountRef                 UNIQUEIDENTIFIER,
    @Metadata                   NVARCHAR(MAX),
    @BoundDate                  DATETIME2(7),
    @SuspendedDate              DATETIME2(7),
    @CanceledDate               DATETIME2(7),
    @ResumeWindowExpirationDate DATETIME2(7),
    @LastAppliedEffectiveDate   DATETIME2(7),
    @CreationDate               DATETIME2(7),
    @RevisionDate               DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[OrganizationPartnershipEntitlement]
    (
        [Id],
        [OrganizationPartnershipId],
        [ExternalId],
        [ExternalIdHash],
        [State],
        [UserId],
        [AccountRef],
        [Metadata],
        [BoundDate],
        [SuspendedDate],
        [CanceledDate],
        [ResumeWindowExpirationDate],
        [LastAppliedEffectiveDate],
        [CreationDate],
        [RevisionDate]
    )
    VALUES
    (
        @Id,
        @OrganizationPartnershipId,
        @ExternalId,
        @ExternalIdHash,
        @State,
        @UserId,
        @AccountRef,
        @Metadata,
        @BoundDate,
        @SuspendedDate,
        @CanceledDate,
        @ResumeWindowExpirationDate,
        @LastAppliedEffectiveDate,
        @CreationDate,
        @RevisionDate
    )
END
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationPartnershipEntitlement_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[OrganizationPartnershipEntitlementView]
    WHERE
        [Id] = @Id
END
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationPartnershipEntitlement_ReadByOrganizationPartnershipIdExternalIdHash]
    @OrganizationPartnershipId UNIQUEIDENTIFIER,
    @ExternalIdHash            VARCHAR(64)
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[OrganizationPartnershipEntitlementView]
    WHERE
        [OrganizationPartnershipId] = @OrganizationPartnershipId
        AND [ExternalIdHash] = @ExternalIdHash
END
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationPartnershipEntitlement_ReadManyCanceledWithExpiredResumeWindow]
    @AsOf DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    -- State 3 is PartnershipEntitlementState.Canceled
    SELECT
        *
    FROM
        [dbo].[OrganizationPartnershipEntitlementView]
    WHERE
        [State] = 3
        AND [UserId] IS NOT NULL
        AND [ResumeWindowExpirationDate] <= @AsOf
END
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationPartnershipEntitlement_Update]
    @Id                         UNIQUEIDENTIFIER,
    @OrganizationPartnershipId  UNIQUEIDENTIFIER,
    @ExternalId                 NVARCHAR(MAX),
    @ExternalIdHash             VARCHAR(64),
    @State                      TINYINT,
    @UserId                     UNIQUEIDENTIFIER,
    @AccountRef                 UNIQUEIDENTIFIER,
    @Metadata                   NVARCHAR(MAX),
    @BoundDate                  DATETIME2(7),
    @SuspendedDate              DATETIME2(7),
    @CanceledDate               DATETIME2(7),
    @ResumeWindowExpirationDate DATETIME2(7),
    @LastAppliedEffectiveDate   DATETIME2(7),
    @CreationDate               DATETIME2(7),
    @RevisionDate               DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[OrganizationPartnershipEntitlement]
    SET
        [OrganizationPartnershipId] = @OrganizationPartnershipId,
        [ExternalId] = @ExternalId,
        [ExternalIdHash] = @ExternalIdHash,
        [State] = @State,
        [UserId] = @UserId,
        [AccountRef] = @AccountRef,
        [Metadata] = @Metadata,
        [BoundDate] = @BoundDate,
        [SuspendedDate] = @SuspendedDate,
        [CanceledDate] = @CanceledDate,
        [ResumeWindowExpirationDate] = @ResumeWindowExpirationDate,
        [LastAppliedEffectiveDate] = @LastAppliedEffectiveDate,
        [CreationDate] = @CreationDate,
        [RevisionDate] = @RevisionDate
    WHERE
        [Id] = @Id
END
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationPartnershipEntitlement_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DELETE
    FROM
        [dbo].[OrganizationPartnershipEntitlement]
    WHERE
        [Id] = @Id
END
GO

CREATE OR ALTER PROCEDURE [dbo].[Organization_DeleteById]
    @Id UNIQUEIDENTIFIER,
    @OrganizationDeleteTasks NVARCHAR(MAX) = NULL
WITH RECOMPILE
AS
BEGIN
    SET NOCOUNT ON

    EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationId] @Id

    DECLARE @BatchSize INT = 100
    WHILE @BatchSize > 0
    BEGIN
        BEGIN TRANSACTION Organization_DeleteById_Ciphers

        DELETE TOP(@BatchSize)
        FROM
            [dbo].[Cipher]
        WHERE
            [UserId] IS NULL
            AND [OrganizationId] = @Id

        SET @BatchSize = @@ROWCOUNT

        COMMIT TRANSACTION Organization_DeleteById_Ciphers
    END

    BEGIN TRANSACTION Organization_DeleteById

    DELETE
    FROM
        [dbo].[AuthRequest]
    WHERE
        [OrganizationId] = @Id

    DELETE
    FROM
        [dbo].[SsoUser]
    WHERE
        [OrganizationId] = @Id

    DELETE
    FROM
        [dbo].[SsoConfig]
    WHERE
        [OrganizationId] = @Id

    DELETE CU
    FROM
        [dbo].[CollectionUser] CU
    INNER JOIN
        [dbo].[OrganizationUser] OU ON [CU].[OrganizationUserId] = [OU].[Id]
    WHERE
        [OU].[OrganizationId] = @Id

    DELETE AP
    FROM
        [dbo].[AccessPolicy] AP
    INNER JOIN
        [dbo].[OrganizationUser] OU ON [AP].[OrganizationUserId] = [OU].[Id]
    WHERE
        [OU].[OrganizationId] = @Id

    DELETE GU
    FROM
        [dbo].[GroupUser] GU
    INNER JOIN
        [dbo].[OrganizationUser] OU ON [GU].[OrganizationUserId] = [OU].[Id]
    WHERE
        [OU].[OrganizationId] = @Id

    DELETE
    FROM
        [dbo].[OrganizationUser]
    WHERE
        [OrganizationId] = @Id

    DELETE
    FROM
         [dbo].[ProviderOrganization]
    WHERE
        [OrganizationId] = @Id

    EXEC [dbo].[OrganizationApiKey_OrganizationDeleted] @Id
    EXEC [dbo].[OrganizationConnection_OrganizationDeleted] @Id
    EXEC [dbo].[OrganizationSponsorship_OrganizationDeleted] @Id
    EXEC [dbo].[OrganizationDomain_OrganizationDeleted] @Id
    EXEC [dbo].[OrganizationIntegration_OrganizationDeleted] @Id

    DELETE
    FROM
        [dbo].[Project]
    WHERE
        [OrganizationId] = @Id

    DELETE
    FROM
        [dbo].[Secret]
    WHERE
        [OrganizationId] = @Id

    DELETE AK
    FROM
        [dbo].[ApiKey] AK
    INNER JOIN
        [dbo].[ServiceAccount] SA ON [AK].[ServiceAccountId] = [SA].[Id]
    WHERE
        [SA].[OrganizationId] = @Id

    DELETE AP
    FROM
        [dbo].[AccessPolicy] AP
    INNER JOIN
        [dbo].[ServiceAccount] SA ON [AP].[GrantedServiceAccountId] = [SA].[Id]
    WHERE
        [SA].[OrganizationId] = @Id

    DELETE
    FROM
        [dbo].[ServiceAccount]
    WHERE
        [OrganizationId] = @Id

    -- Delete Notification Status
    DELETE
        NS
    FROM
        [dbo].[NotificationStatus] NS
    INNER JOIN
        [dbo].[Notification] N ON N.[Id] = NS.[NotificationId]
    WHERE
        N.[OrganizationId] = @Id

    -- Delete Notification
    DELETE
    FROM
        [dbo].[Notification]
    WHERE
        [OrganizationId] = @Id

    -- Delete Organization Application
    DELETE
    FROM
        [dbo].[OrganizationApplication]
    WHERE
        [OrganizationId] = @Id

    -- Delete Organization Report
    DELETE
    FROM
        [dbo].[OrganizationReport]
    WHERE
        [OrganizationId] = @Id

    -- Delete Organization Owned Sends
    DELETE
    FROM
        [dbo].[Send]
    WHERE
        [OrganizationId] = @Id

    -- Delete Organization Partnership Entitlements
    DELETE
        OPE
    FROM
        [dbo].[OrganizationPartnershipEntitlement] OPE
    INNER JOIN
        [dbo].[OrganizationPartnership] OP ON OP.[Id] = OPE.[OrganizationPartnershipId]
    WHERE
        OP.[OrganizationId] = @Id

    -- Delete Organization Partnership
    DELETE
    FROM
        [dbo].[OrganizationPartnership]
    WHERE
        [OrganizationId] = @Id

    -- Atomically enqueue one or more OrganizationDeleteTasks (e.g. for purging Table
    -- Storage event logs) so downstream cleanup is durably recorded with the deletion.
    -- Tasks are passed as a JSON array of { Id, TaskType, CreationDate } objects, letting
    -- any number of teams enqueue their own cleanup type in the same transaction as the delete.
    IF @OrganizationDeleteTasks IS NOT NULL
    BEGIN
        -- [TaskType] and [CreationDate] are read straight from OPENJSON without the ISNULL
        -- fallbacks that bulk JSON procedures normally need. That guidance covers adding a
        -- NOT NULL column to an existing table, where an older server omits the new field and
        -- OPENJSON yields NULL. Here the parameter, the JSON contract, and the table all ship
        -- together: an older server sends no JSON at all, so the guard above skips the insert.
        -- An explicit NULL [TaskType] should fail rather than silently enqueue task type 0.
        INSERT INTO [dbo].[OrganizationDeleteTask]
        (
            [Id],
            [OrganizationId],
            [TaskType],
            [CreationDate],
            [RevisionDate]
        )
        SELECT
            [Id],
            @Id,
            [TaskType],
            [CreationDate],
            [CreationDate]
        FROM
            OPENJSON(@OrganizationDeleteTasks)
            WITH (
                [Id]           UNIQUEIDENTIFIER '$.Id',
                [TaskType]     TINYINT          '$.TaskType',
                [CreationDate] DATETIME2(7)     '$.CreationDate'
            )
    END

    DELETE
    FROM
        [dbo].[Organization]
    WHERE
        [Id] = @Id

    COMMIT TRANSACTION Organization_DeleteById
END
GO

CREATE OR ALTER PROCEDURE [dbo].[User_DeleteById]
    @Id UNIQUEIDENTIFIER
WITH RECOMPILE
AS
BEGIN
    SET NOCOUNT ON
    DECLARE @BatchSize INT = 100

    -- Delete ciphers
    WHILE @BatchSize > 0
    BEGIN
        BEGIN TRANSACTION User_DeleteById_Ciphers

        DELETE TOP(@BatchSize)
        FROM
            [dbo].[Cipher]
        WHERE
            [UserId] = @Id

        SET @BatchSize = @@ROWCOUNT

        COMMIT TRANSACTION User_DeleteById_Ciphers
    END

    BEGIN TRANSACTION User_DeleteById

    -- Delete WebAuthnCredentials
    DELETE
    FROM
        [dbo].[WebAuthnCredential]
    WHERE
        [UserId] = @Id

    -- Delete folders
    DELETE
    FROM
        [dbo].[Folder]
    WHERE
        [UserId] = @Id

    -- Delete AuthRequest, must be before Device
    DELETE
    FROM
        [dbo].[AuthRequest]
    WHERE 
        [UserId] = @Id

    -- Delete devices
    DELETE
    FROM
        [dbo].[Device]
    WHERE
        [UserId] = @Id

    -- Migrate DefaultUserCollection to SharedCollection before deleting CollectionUser records
    DECLARE @OrgUserIds [dbo].[GuidIdArray]
    INSERT INTO @OrgUserIds (Id)
    SELECT [Id] FROM [dbo].[OrganizationUser] WHERE [UserId] = @Id
    
    IF EXISTS (SELECT 1 FROM @OrgUserIds)
    BEGIN
        EXEC [dbo].[OrganizationUser_MigrateDefaultCollection] @OrgUserIds
    END

    -- Delete collection users
    DELETE
        CU
    FROM
        [dbo].[CollectionUser] CU
        INNER JOIN
        [dbo].[OrganizationUser] OU ON OU.[Id] = CU.[OrganizationUserId]
    WHERE
        OU.[UserId] = @Id

    -- Delete group users
    DELETE
        GU
    FROM
        [dbo].[GroupUser] GU
        INNER JOIN
        [dbo].[OrganizationUser] OU ON OU.[Id] = GU.[OrganizationUserId]
    WHERE
        OU.[UserId] = @Id

    -- Delete AccessPolicy
    DELETE
        AP
    FROM
        [dbo].[AccessPolicy] AP
        INNER JOIN
        [dbo].[OrganizationUser] OU ON OU.[Id] = AP.[OrganizationUserId]
    WHERE
        [UserId] = @Id

    -- Delete organization users
    DELETE
    FROM
        [dbo].[OrganizationUser]
    WHERE
        [UserId] = @Id

    -- Delete provider users
    DELETE
    FROM
        [dbo].[ProviderUser]
    WHERE
        [UserId] = @Id

    -- Delete SSO Users
    DELETE
    FROM
        [dbo].[SsoUser]
    WHERE
        [UserId] = @Id

    -- Delete Emergency Accesses
    DELETE
    FROM
        [dbo].[EmergencyAccess]
    WHERE
        [GrantorId] = @Id
        OR
        [GranteeId] = @Id

    -- Delete Sends
    DELETE
    FROM
        [dbo].[Send]
    WHERE 
        [UserId] = @Id

    -- Delete Notification Status
    DELETE
    FROM
        [dbo].[NotificationStatus]
    WHERE
        [UserId] = @Id

    -- Delete Notification
    DELETE
    FROM
        [dbo].[Notification]
    WHERE
        [UserId] = @Id

    -- Unbind Organization Partnership Entitlements
    UPDATE
        [dbo].[OrganizationPartnershipEntitlement]
    SET
        [UserId] = NULL,
        [AccountRef] = NULL
    WHERE
        [UserId] = @Id

    -- Finally, delete the user
    DELETE
    FROM
        [dbo].[User]
    WHERE
        [Id] = @Id

    COMMIT TRANSACTION User_DeleteById
END
GO

CREATE OR ALTER PROCEDURE [dbo].[User_DeleteByIds]
    @Ids NVARCHAR(MAX)
WITH RECOMPILE
AS
BEGIN
    SET NOCOUNT ON
    -- Declare a table variable to hold the parsed JSON data
    DECLARE @ParsedIds TABLE (Id UNIQUEIDENTIFIER);

    -- Parse the JSON input into the table variable
    INSERT INTO @ParsedIds (Id)
    SELECT value
    FROM OPENJSON(@Ids);

    -- Check if the input table is empty
    IF (SELECT COUNT(1) FROM @ParsedIds) < 1
    BEGIN
        RETURN(-1);
    END

    DECLARE @BatchSize INT = 100

    -- Delete ciphers
    WHILE @BatchSize > 0
    BEGIN
        BEGIN TRANSACTION User_DeleteById_Ciphers

        DELETE TOP(@BatchSize)
        FROM
            [dbo].[Cipher]
        WHERE
            [UserId] IN (SELECT * FROM @ParsedIds)

        SET @BatchSize = @@ROWCOUNT

        COMMIT TRANSACTION User_DeleteById_Ciphers
    END

    BEGIN TRANSACTION User_DeleteById

    -- Delete WebAuthnCredentials
    DELETE
    FROM
        [dbo].[WebAuthnCredential]
    WHERE
        [UserId] IN (SELECT * FROM @ParsedIds)

    -- Delete folders
    DELETE
    FROM
        [dbo].[Folder]
    WHERE
        [UserId] IN (SELECT * FROM @ParsedIds)

    -- Delete AuthRequest, must be before Device
    DELETE
    FROM
        [dbo].[AuthRequest]
    WHERE 
        [UserId] IN (SELECT * FROM @ParsedIds)

    -- Delete devices
    DELETE
    FROM
        [dbo].[Device]
    WHERE
        [UserId] IN (SELECT * FROM @ParsedIds)

    -- Migrate DefaultUserCollection to SharedCollection before deleting CollectionUser records
    DECLARE @OrgUserIds [dbo].[GuidIdArray]
    INSERT INTO @OrgUserIds (Id)
    SELECT [Id] FROM [dbo].[OrganizationUser] WHERE [UserId] IN (SELECT * FROM @ParsedIds)
    
    IF EXISTS (SELECT 1 FROM @OrgUserIds)
    BEGIN
        EXEC [dbo].[OrganizationUser_MigrateDefaultCollection] @OrgUserIds
    END

    -- Delete collection users
    DELETE
        CU
    FROM
        [dbo].[CollectionUser] CU
        INNER JOIN
        [dbo].[OrganizationUser] OU ON OU.[Id] = CU.[OrganizationUserId]
    WHERE
        OU.[UserId] IN (SELECT * FROM @ParsedIds)

    -- Delete group users
    DELETE
        GU
    FROM
        [dbo].[GroupUser] GU
        INNER JOIN
        [dbo].[OrganizationUser] OU ON OU.[Id] = GU.[OrganizationUserId]
    WHERE
        OU.[UserId] IN (SELECT * FROM @ParsedIds)

    -- Delete AccessPolicy
    DELETE
        AP
    FROM
        [dbo].[AccessPolicy] AP
        INNER JOIN
        [dbo].[OrganizationUser] OU ON OU.[Id] = AP.[OrganizationUserId]
    WHERE
        [UserId] IN (SELECT * FROM @ParsedIds)

    -- Delete organization users
    DELETE
    FROM
        [dbo].[OrganizationUser]
    WHERE
        [UserId] IN (SELECT * FROM @ParsedIds)

    -- Delete provider users
    DELETE
    FROM
        [dbo].[ProviderUser]
    WHERE
        [UserId] IN (SELECT * FROM @ParsedIds)

    -- Delete SSO Users
    DELETE
    FROM
        [dbo].[SsoUser]
    WHERE
        [UserId] IN (SELECT * FROM @ParsedIds)

    -- Delete Emergency Accesses
    DELETE
    FROM
        [dbo].[EmergencyAccess]
    WHERE
        [GrantorId] IN (SELECT * FROM @ParsedIds)
        OR
        [GranteeId] IN (SELECT * FROM @ParsedIds)

    -- Delete Sends
    DELETE
    FROM
        [dbo].[Send]
    WHERE 
        [UserId] IN (SELECT * FROM @ParsedIds)

    -- Delete Notification Status
    DELETE
    FROM
        [dbo].[NotificationStatus]
    WHERE
        [UserId] IN (SELECT * FROM @ParsedIds)

    -- Delete Notification
    DELETE
    FROM
        [dbo].[Notification]
    WHERE
        [UserId] IN (SELECT * FROM @ParsedIds)

    -- Unbind Organization Partnership Entitlements
    UPDATE
        [dbo].[OrganizationPartnershipEntitlement]
    SET
        [UserId] = NULL,
        [AccountRef] = NULL
    WHERE
        [UserId] IN (SELECT * FROM @ParsedIds)

    -- Finally, delete the user
    DELETE
    FROM
        [dbo].[User]
    WHERE
        [Id] IN (SELECT * FROM @ParsedIds)

    COMMIT TRANSACTION User_DeleteById
END
GO
