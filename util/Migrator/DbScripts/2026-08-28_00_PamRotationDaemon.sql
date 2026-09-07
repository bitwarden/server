-- PAM rotation daemon surface: tables/procedures, AccessAuditEvent rotation columns, and the AccessLease expiry sweep.

IF OBJECT_ID('[dbo].[PamTargetSystem]') IS NULL
BEGIN
    CREATE TABLE [dbo].[PamTargetSystem] (
        [Id]                            UNIQUEIDENTIFIER    NOT NULL,
        [OrganizationId]                UNIQUEIDENTIFIER    NOT NULL,
        [Name]                          NVARCHAR(200)        NOT NULL,
        [Method]                        TINYINT              NOT NULL,
        [Kind]                          TINYINT              NULL,
        [PasswordPolicy]                NVARCHAR(2000)       NULL,
        [SupportsSessionTermination]    BIT                  NULL,
        [Status]                        TINYINT              NOT NULL,
        [CreationDate]                  DATETIME2(7)         NOT NULL,
        [RevisionDate]                  DATETIME2(7)         NOT NULL,
        CONSTRAINT [PK_PamTargetSystem] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_PamTargetSystem_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id]) ON DELETE CASCADE
    );
END
GO

IF OBJECT_ID('[dbo].[PamDaemon]') IS NULL
BEGIN
    CREATE TABLE [dbo].[PamDaemon] (
        [Id]                UNIQUEIDENTIFIER    NOT NULL,
        [OrganizationId]    UNIQUEIDENTIFIER    NOT NULL,
        [Name]              NVARCHAR(200)       NOT NULL,
        [ApiKeyId]          UNIQUEIDENTIFIER    NOT NULL,
        [Status]            TINYINT             NOT NULL,
        [LastHeartbeatAt]   DATETIME2(7)        NULL,
        [CreationDate]      DATETIME2(7)        NOT NULL,
        [RevisionDate]      DATETIME2(7)        NOT NULL,
        CONSTRAINT [PK_PamDaemon] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_PamDaemon_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_PamDaemon_ApiKey] FOREIGN KEY ([ApiKeyId]) REFERENCES [dbo].[ApiKey] ([Id]) ON DELETE NO ACTION
    );

    CREATE UNIQUE NONCLUSTERED INDEX [IX_PamDaemon_ApiKeyId]
        ON [dbo].[PamDaemon] ([ApiKeyId] ASC);

    CREATE NONCLUSTERED INDEX [IX_PamDaemon_OrganizationId]
        ON [dbo].[PamDaemon] ([OrganizationId] ASC);
END
GO

IF OBJECT_ID('[dbo].[PamDaemonTargetAssignment]') IS NULL
BEGIN
    CREATE TABLE [dbo].[PamDaemonTargetAssignment] (
        [Id]                UNIQUEIDENTIFIER    NOT NULL,
        [DaemonId]          UNIQUEIDENTIFIER    NOT NULL,
        [TargetSystemId]    UNIQUEIDENTIFIER    NOT NULL,
        [OrganizationId]    UNIQUEIDENTIFIER    NOT NULL,
        [CreationDate]      DATETIME2(7)        NOT NULL,
        CONSTRAINT [PK_PamDaemonTargetAssignment] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_PamDaemonTargetAssignment_Daemon] FOREIGN KEY ([DaemonId]) REFERENCES [dbo].[PamDaemon] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PamDaemonTargetAssignment_TargetSystem] FOREIGN KEY ([TargetSystemId]) REFERENCES [dbo].[PamTargetSystem] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PamDaemonTargetAssignment_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id]) ON DELETE CASCADE
    );

    CREATE UNIQUE NONCLUSTERED INDEX [IX_PamDaemonTargetAssignment_DaemonId_TargetSystemId]
        ON [dbo].[PamDaemonTargetAssignment] ([DaemonId] ASC, [TargetSystemId] ASC);

    CREATE NONCLUSTERED INDEX [IX_PamDaemonTargetAssignment_TargetSystemId]
        ON [dbo].[PamDaemonTargetAssignment] ([TargetSystemId] ASC);
END
GO

IF OBJECT_ID('[dbo].[PamRotationConfig]') IS NULL
BEGIN
    CREATE TABLE [dbo].[PamRotationConfig] (
        [Id]                    UNIQUEIDENTIFIER    NOT NULL,
        [OrganizationId]        UNIQUEIDENTIFIER    NOT NULL,
        [CipherId]              UNIQUEIDENTIFIER    NOT NULL,
        [TargetSystemId]        UNIQUEIDENTIFIER    NOT NULL,
        [AccountIdentity]       NVARCHAR(500)       NOT NULL,
        [TerminateSessions]     BIT                 NOT NULL,
        [ScheduleCron]          NVARCHAR(100)       NULL,
        [RotateOnAccessEnd]     BIT                 NOT NULL,
        [NextRotationAt]        DATETIME2(7)        NULL,
        [Enabled]               BIT                 NOT NULL,
        [LastRotationAt]        DATETIME2(7)        NULL,
        [CreationDate]          DATETIME2(7)        NOT NULL,
        [RevisionDate]          DATETIME2(7)        NOT NULL,
        CONSTRAINT [PK_PamRotationConfig] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_PamRotationConfig_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_PamRotationConfig_TargetSystem] FOREIGN KEY ([TargetSystemId]) REFERENCES [dbo].[PamTargetSystem] ([Id]) ON DELETE NO ACTION
    );

    -- OneConfigPerCipher.
    CREATE UNIQUE NONCLUSTERED INDEX [IX_PamRotationConfig_CipherId]
        ON [dbo].[PamRotationConfig] ([CipherId] ASC);

    -- Backs the due-rotation sweep (PamRotationConfig_ReadManyDue).
    CREATE NONCLUSTERED INDEX [IX_PamRotationConfig_NextRotationAt]
        ON [dbo].[PamRotationConfig] ([NextRotationAt] ASC)
        WHERE [Enabled] = 1 AND [NextRotationAt] IS NOT NULL;
END
GO

IF OBJECT_ID('[dbo].[PamRotationJob]') IS NULL
BEGIN
    CREATE TABLE [dbo].[PamRotationJob] (
        [Id]                    UNIQUEIDENTIFIER    NOT NULL,
        [RotationConfigId]      UNIQUEIDENTIFIER    NOT NULL,
        [Source]                TINYINT             NOT NULL,
        [Status]                TINYINT             NOT NULL,
        [ClaimedByDaemonId]     UNIQUEIDENTIFIER    NULL,
        [ClaimedAt]             DATETIME2(7)        NULL,
        [CreationDate]          DATETIME2(7)        NOT NULL,
        [NextClaimableAt]       DATETIME2(7)        NOT NULL,
        [ExpiresAt]             DATETIME2(7)        NOT NULL,
        CONSTRAINT [PK_PamRotationJob] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_PamRotationJob_RotationConfig] FOREIGN KEY ([RotationConfigId]) REFERENCES [dbo].[PamRotationConfig] ([Id]) ON DELETE NO ACTION
    );

    CREATE NONCLUSTERED INDEX [IX_PamRotationJob_RotationConfigId_Status]
        ON [dbo].[PamRotationJob] ([RotationConfigId] ASC, [Status] ASC);

    CREATE NONCLUSTERED INDEX [IX_PamRotationJob_Status_ExpiresAt]
        ON [dbo].[PamRotationJob] ([Status] ASC, [ExpiresAt] ASC);

    CREATE NONCLUSTERED INDEX [IX_PamRotationJob_ClaimedByDaemonId_Status]
        ON [dbo].[PamRotationJob] ([ClaimedByDaemonId] ASC, [Status] ASC);
END
GO

IF OBJECT_ID('[dbo].[PamRotationAttempt]') IS NULL
BEGIN
    CREATE TABLE [dbo].[PamRotationAttempt] (
        [Id]                     UNIQUEIDENTIFIER    NOT NULL,
        [JobId]                  UNIQUEIDENTIFIER    NOT NULL,
        [ClaimedByDaemonId]      UNIQUEIDENTIFIER    NOT NULL,
        [CipherUpdated]          BIT                 NOT NULL,
        [Status]                 TINYINT             NOT NULL,
        [FailureReason]          NVARCHAR(500)       NULL,
        [SyncState]              TINYINT             NULL,
        [SessionTermination]     TINYINT             NULL,
        [CreationDate]           DATETIME2(7)        NOT NULL,
        [ResolvedDate]           DATETIME2(7)        NULL,
        CONSTRAINT [PK_PamRotationAttempt] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_PamRotationAttempt_RotationJob] FOREIGN KEY ([JobId]) REFERENCES [dbo].[PamRotationJob] ([Id])
    );

    CREATE NONCLUSTERED INDEX [IX_PamRotationAttempt_JobId_Status]
        ON [dbo].[PamRotationAttempt] ([JobId] ASC, [Status] ASC);
END
GO

-- One row per expired lease; LeaseExpired fires exactly one audit event per lease.
IF OBJECT_ID('[dbo].[PamLeaseExpirySweep]') IS NULL
BEGIN
    CREATE TABLE [dbo].[PamLeaseExpirySweep] (
        [AccessLeaseId] UNIQUEIDENTIFIER    NOT NULL,
        [SweptDate]     DATETIME2 (7)       NOT NULL,
        CONSTRAINT [PK_PamLeaseExpirySweep] PRIMARY KEY CLUSTERED ([AccessLeaseId] ASC),
        CONSTRAINT [FK_PamLeaseExpirySweep_AccessLease] FOREIGN KEY ([AccessLeaseId]) REFERENCES [dbo].[AccessLease] ([Id]) ON DELETE CASCADE
    );
END
GO

-- OrganizationId indexes for these three tables' _ReadByOrganizationId procedures and cascade-delete FK scans.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'IX_PamTargetSystem_OrganizationId' AND [object_id] = OBJECT_ID('[dbo].[PamTargetSystem]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_PamTargetSystem_OrganizationId]
        ON [dbo].[PamTargetSystem] ([OrganizationId] ASC);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'IX_PamDaemonTargetAssignment_OrganizationId' AND [object_id] = OBJECT_ID('[dbo].[PamDaemonTargetAssignment]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_PamDaemonTargetAssignment_OrganizationId]
        ON [dbo].[PamDaemonTargetAssignment] ([OrganizationId] ASC);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'IX_PamRotationConfig_OrganizationId' AND [object_id] = OBJECT_ID('[dbo].[PamRotationConfig]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_PamRotationConfig_OrganizationId]
        ON [dbo].[PamRotationConfig] ([OrganizationId] ASC);
END
GO

-- Backs the daemon poll's assignment -> config join and PamRotationConfig_AnyByTargetSystemWithTerminateSessions.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'IX_PamRotationConfig_TargetSystemId' AND [object_id] = OBJECT_ID('[dbo].[PamRotationConfig]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_PamRotationConfig_TargetSystemId]
        ON [dbo].[PamRotationConfig] ([TargetSystemId] ASC);
END
GO


-- Stored procedures

CREATE OR ALTER PROCEDURE [dbo].[PamTargetSystem_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @OrganizationId UNIQUEIDENTIFIER,
    @Name NVARCHAR(200),
    @Method TINYINT,
    @Kind TINYINT = NULL,
    @PasswordPolicy NVARCHAR(2000) = NULL,
    @SupportsSessionTermination BIT = NULL,
    @Status TINYINT,
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[PamTargetSystem]
    (
        [Id],
        [OrganizationId],
        [Name],
        [Method],
        [Kind],
        [PasswordPolicy],
        [SupportsSessionTermination],
        [Status],
        [CreationDate],
        [RevisionDate]
    )
    VALUES
    (
        @Id,
        @OrganizationId,
        @Name,
        @Method,
        @Kind,
        @PasswordPolicy,
        @SupportsSessionTermination,
        @Status,
        @CreationDate,
        @RevisionDate
    )
END
GO

CREATE OR ALTER PROCEDURE [dbo].[PamTargetSystem_Update]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @Name NVARCHAR(200),
    @Method TINYINT,
    @Kind TINYINT = NULL,
    @PasswordPolicy NVARCHAR(2000) = NULL,
    @SupportsSessionTermination BIT = NULL,
    @Status TINYINT,
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[PamTargetSystem]
    SET
        [OrganizationId] = @OrganizationId,
        [Name] = @Name,
        [Method] = @Method,
        [Kind] = @Kind,
        [PasswordPolicy] = @PasswordPolicy,
        [SupportsSessionTermination] = @SupportsSessionTermination,
        [Status] = @Status,
        [CreationDate] = @CreationDate,
        [RevisionDate] = @RevisionDate
    WHERE
        [Id] = @Id
END
GO

CREATE OR ALTER PROCEDURE [dbo].[PamTargetSystem_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT *
    FROM [dbo].[PamTargetSystem]
    WHERE [Id] = @Id
END
GO

CREATE OR ALTER PROCEDURE [dbo].[PamTargetSystem_ReadByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT *
    FROM [dbo].[PamTargetSystem]
    WHERE [OrganizationId] = @OrganizationId
END
GO

CREATE OR ALTER PROCEDURE [dbo].[PamTargetSystem_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    -- No cascade cleanup; a referenced target system is blocked by its NO ACTION FKs.
    DELETE FROM [dbo].[PamTargetSystem]
    WHERE [Id] = @Id
END
GO

CREATE OR ALTER PROCEDURE [dbo].[PamDaemon_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @OrganizationId UNIQUEIDENTIFIER,
    @Name NVARCHAR(200),
    @ApiKeyId UNIQUEIDENTIFIER,
    @Status TINYINT,
    @LastHeartbeatAt DATETIME2(7) = NULL,
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[PamDaemon]
    (
        [Id],
        [OrganizationId],
        [Name],
        [ApiKeyId],
        [Status],
        [LastHeartbeatAt],
        [CreationDate],
        [RevisionDate]
    )
    VALUES
    (
        @Id,
        @OrganizationId,
        @Name,
        @ApiKeyId,
        @Status,
        @LastHeartbeatAt,
        @CreationDate,
        @RevisionDate
    )
END
GO

CREATE OR ALTER PROCEDURE [dbo].[PamDaemon_Update]
    @Id UNIQUEIDENTIFIER,
    @Name NVARCHAR(200),
    @Status TINYINT,
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    -- Name + Status only; LastHeartbeatAt is bumped separately (PamDaemon_UpdateHeartbeat) to avoid racing a poll.
    UPDATE
        [dbo].[PamDaemon]
    SET
        [Name] = @Name,
        [Status] = @Status,
        [RevisionDate] = @RevisionDate
    WHERE
        [Id] = @Id
END
GO

CREATE OR ALTER PROCEDURE [dbo].[PamDaemon_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT *
    FROM [dbo].[PamDaemon]
    WHERE [Id] = @Id
END
GO

CREATE OR ALTER PROCEDURE [dbo].[PamDaemon_ReadByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT *
    FROM [dbo].[PamDaemon]
    WHERE [OrganizationId] = @OrganizationId
END
GO

CREATE OR ALTER PROCEDURE [dbo].[PamDaemon_UpdateHeartbeat]
    @Id UNIQUEIDENTIFIER,
    @Now DATETIME2(7),
    @MinIntervalSeconds INT
AS
BEGIN
    SET NOCOUNT ON

    -- Conditional bump: the WHERE guard no-ops calls within @MinIntervalSeconds of the last heartbeat.
    UPDATE [dbo].[PamDaemon]
    SET [LastHeartbeatAt] = @Now
    WHERE [Id] = @Id
        AND ([LastHeartbeatAt] IS NULL OR [LastHeartbeatAt] < DATEADD(SECOND, -@MinIntervalSeconds, @Now))
END
GO

CREATE OR ALTER PROCEDURE [dbo].[PamDaemonDetails_ReadByApiKeyId]
    @ApiKeyId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    -- Includes org Enabled/UsePam flags so a lapsed org can't mint a token.
    SELECT
        D.*,
        O.[Enabled] AS [OrganizationEnabled],
        O.[UsePam] AS [OrganizationUsePam]
    FROM [dbo].[PamDaemon] D
    INNER JOIN [dbo].[Organization] O ON O.[Id] = D.[OrganizationId]
    WHERE D.[ApiKeyId] = @ApiKeyId
END
GO

CREATE OR ALTER PROCEDURE [dbo].[PamDaemonTargetAssignment_Create]
    @Id UNIQUEIDENTIFIER,
    @DaemonId UNIQUEIDENTIFIER,
    @TargetSystemId UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @CreationDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    -- @Id is a plain input, not OUTPUT; the caller assigns it beforehand.
    INSERT INTO [dbo].[PamDaemonTargetAssignment]
    (
        [Id],
        [DaemonId],
        [TargetSystemId],
        [OrganizationId],
        [CreationDate]
    )
    VALUES
    (
        @Id,
        @DaemonId,
        @TargetSystemId,
        @OrganizationId,
        @CreationDate
    )
END
GO

CREATE OR ALTER PROCEDURE [dbo].[PamDaemonTargetAssignment_DeleteByDaemonIdTargetSystemId]
    @DaemonId UNIQUEIDENTIFIER,
    @TargetSystemId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DELETE FROM [dbo].[PamDaemonTargetAssignment]
    WHERE [DaemonId] = @DaemonId AND [TargetSystemId] = @TargetSystemId
END
GO

CREATE OR ALTER PROCEDURE [dbo].[PamDaemonTargetAssignment_ReadByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT *
    FROM [dbo].[PamDaemonTargetAssignment]
    WHERE [OrganizationId] = @OrganizationId
END
GO

CREATE OR ALTER PROCEDURE [dbo].[PamDaemonTargetAssignment_ReadByDaemonId]
    @DaemonId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT *
    FROM [dbo].[PamDaemonTargetAssignment]
    WHERE [DaemonId] = @DaemonId
END
GO

CREATE OR ALTER PROCEDURE [dbo].[PamDaemonTargetAssignment_ExistsByDaemonIdTargetSystemId]
    @DaemonId UNIQUEIDENTIFIER,
    @TargetSystemId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT 1
    FROM [dbo].[PamDaemonTargetAssignment]
    WHERE [DaemonId] = @DaemonId AND [TargetSystemId] = @TargetSystemId
END
GO

CREATE OR ALTER PROCEDURE [dbo].[PamRotationConfig_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @OrganizationId UNIQUEIDENTIFIER,
    @CipherId UNIQUEIDENTIFIER,
    @TargetSystemId UNIQUEIDENTIFIER,
    @AccountIdentity NVARCHAR(500),
    @TerminateSessions BIT,
    @ScheduleCron NVARCHAR(100) = NULL,
    @RotateOnAccessEnd BIT,
    @NextRotationAt DATETIME2(7) = NULL,
    @Enabled BIT,
    @LastRotationAt DATETIME2(7) = NULL,
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[PamRotationConfig]
    (
        [Id],
        [OrganizationId],
        [CipherId],
        [TargetSystemId],
        [AccountIdentity],
        [TerminateSessions],
        [ScheduleCron],
        [RotateOnAccessEnd],
        [NextRotationAt],
        [Enabled],
        [LastRotationAt],
        [CreationDate],
        [RevisionDate]
    )
    VALUES
    (
        @Id,
        @OrganizationId,
        @CipherId,
        @TargetSystemId,
        @AccountIdentity,
        @TerminateSessions,
        @ScheduleCron,
        @RotateOnAccessEnd,
        @NextRotationAt,
        @Enabled,
        @LastRotationAt,
        @CreationDate,
        @RevisionDate
    )
END
GO

CREATE OR ALTER PROCEDURE [dbo].[PamRotationConfig_Update]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @CipherId UNIQUEIDENTIFIER,
    @TargetSystemId UNIQUEIDENTIFIER,
    @AccountIdentity NVARCHAR(500),
    @TerminateSessions BIT,
    @ScheduleCron NVARCHAR(100) = NULL,
    @RotateOnAccessEnd BIT,
    @NextRotationAt DATETIME2(7) = NULL,
    @Enabled BIT,
    @LastRotationAt DATETIME2(7) = NULL,
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[PamRotationConfig]
    SET
        [OrganizationId] = @OrganizationId,
        [CipherId] = @CipherId,
        [TargetSystemId] = @TargetSystemId,
        [AccountIdentity] = @AccountIdentity,
        [TerminateSessions] = @TerminateSessions,
        [ScheduleCron] = @ScheduleCron,
        [RotateOnAccessEnd] = @RotateOnAccessEnd,
        [NextRotationAt] = @NextRotationAt,
        [Enabled] = @Enabled,
        [LastRotationAt] = @LastRotationAt,
        [CreationDate] = @CreationDate,
        [RevisionDate] = @RevisionDate
    WHERE
        [Id] = @Id
END
GO

CREATE OR ALTER PROCEDURE [dbo].[PamRotationConfig_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT *
    FROM [dbo].[PamRotationConfig]
    WHERE [Id] = @Id
END
GO

CREATE OR ALTER PROCEDURE [dbo].[PamRotationConfig_ReadByCipherId]
    @CipherId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    -- OneConfigPerCipher: at most one row can ever match.
    SELECT *
    FROM [dbo].[PamRotationConfig]
    WHERE [CipherId] = @CipherId
END
GO

CREATE OR ALTER PROCEDURE [dbo].[PamRotationConfig_ReadDetailsById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    -- Denormalizes target name/method and computes HasActiveJob to avoid a second round trip.
    SELECT
        C.*,
        T.[Name] AS [TargetSystemName],
        T.[Method] AS [TargetSystemMethod],
        CASE WHEN EXISTS (
            SELECT 1
            FROM [dbo].[PamRotationJob] J
            WHERE J.[RotationConfigId] = C.[Id] AND J.[Status] IN (0, 1) -- Pending, Claimed
        ) THEN 1 ELSE 0 END AS [HasActiveJob]
    FROM [dbo].[PamRotationConfig] C
    INNER JOIN [dbo].[PamTargetSystem] T ON T.[Id] = C.[TargetSystemId]
    WHERE C.[Id] = @Id
END
GO

CREATE OR ALTER PROCEDURE [dbo].[PamRotationConfig_ReadManyByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    -- Denormalizes the target's name/method and computes HasActiveJob for the org's schedule-list view.
    SELECT
        C.*,
        T.[Name] AS [TargetSystemName],
        T.[Method] AS [TargetSystemMethod],
        CASE WHEN EXISTS (
            SELECT 1
            FROM [dbo].[PamRotationJob] J
            WHERE J.[RotationConfigId] = C.[Id] AND J.[Status] IN (0, 1) -- Pending, Claimed
        ) THEN 1 ELSE 0 END AS [HasActiveJob]
    FROM [dbo].[PamRotationConfig] C
    INNER JOIN [dbo].[PamTargetSystem] T ON T.[Id] = C.[TargetSystemId]
    WHERE C.[OrganizationId] = @OrganizationId
END
GO

CREATE OR ALTER PROCEDURE [dbo].[PamRotationConfig_ReadManyDue]
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    -- Due phase: enabled, automatic, active-target configs past schedule with no job in flight.
    SELECT C.*
    FROM [dbo].[PamRotationConfig] C
    INNER JOIN [dbo].[PamTargetSystem] T ON T.[Id] = C.[TargetSystemId]
    WHERE C.[Enabled] = 1
        AND C.[NextRotationAt] IS NOT NULL
        AND C.[NextRotationAt] <= @Now
        AND T.[Method] = 0 -- Automatic
        AND T.[Status] = 0 -- Active
        AND NOT EXISTS (
            SELECT 1
            FROM [dbo].[PamRotationJob] J
            WHERE J.[RotationConfigId] = C.[Id] AND J.[Status] IN (0, 1) -- Pending, Claimed
        )
END
GO

CREATE OR ALTER PROCEDURE [dbo].[PamRotationConfig_DeleteWithJobs]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON
    -- Hard-deletes jobs/attempts before the config; both child FKs are ON DELETE NO ACTION.
    SET XACT_ABORT ON

    BEGIN TRANSACTION

    -- Re-checks under PamRotationJob_Create's range lock; caller's HasActiveJob read was outside this transaction.
    IF EXISTS (
        SELECT 1
        FROM [dbo].[PamRotationJob] WITH (UPDLOCK, HOLDLOCK)
        WHERE [RotationConfigId] = @Id
            AND [Status] IN (0, 1) -- Pending, Claimed
    )
    BEGIN
        ROLLBACK TRANSACTION
        SELECT 0 -- ActiveJobExists
        RETURN
    END

    DELETE A
    FROM [dbo].[PamRotationAttempt] A
    INNER JOIN [dbo].[PamRotationJob] J ON J.[Id] = A.[JobId]
    WHERE J.[RotationConfigId] = @Id

    DELETE FROM [dbo].[PamRotationJob]
    WHERE [RotationConfigId] = @Id

    DELETE FROM [dbo].[PamRotationConfig]
    WHERE [Id] = @Id

    COMMIT TRANSACTION

    SELECT 1 -- Deleted
END
GO

CREATE OR ALTER PROCEDURE [dbo].[PamRotationConfig_AnyByTargetSystemWithTerminateSessions]
    @TargetSystemId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    -- Capability-withdrawal guard: blocks disabling SupportsSessionTermination while any config still uses TerminateSessions.
    SELECT 1
    FROM [dbo].[PamRotationConfig]
    WHERE [TargetSystemId] = @TargetSystemId AND [TerminateSessions] = 1
END
GO

CREATE OR ALTER PROCEDURE [dbo].[PamRotationJob_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT *
    FROM [dbo].[PamRotationJob]
    WHERE [Id] = @Id
END
GO

CREATE OR ALTER PROCEDURE [dbo].[PamRotationJob_ReadManyByConfigId]
    @RotationConfigId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    -- Two result sets let the caller zip jobs to attempts without an N+1.
    SELECT *
    FROM [dbo].[PamRotationJob]
    WHERE [RotationConfigId] = @RotationConfigId
    ORDER BY [CreationDate] DESC

    SELECT A.*
    FROM [dbo].[PamRotationAttempt] A
    INNER JOIN [dbo].[PamRotationJob] J ON J.[Id] = A.[JobId]
    WHERE J.[RotationConfigId] = @RotationConfigId
    ORDER BY A.[JobId], A.[CreationDate] ASC
END
GO

CREATE OR ALTER PROCEDURE [dbo].[PamRotationAttempt_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT *
    FROM [dbo].[PamRotationAttempt]
    WHERE [Id] = @Id
END
GO

CREATE OR ALTER PROCEDURE [dbo].[PamRotationJob_ReadManyClaimableByDaemonId]
    @DaemonId UNIQUEIDENTIFIER,
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    -- Mirrors every eligibility condition PamRotationJob_Claim checks, so the two can't diverge.
    SELECT J.*, C.[TargetSystemId]
    FROM [dbo].[PamRotationJob] J
    INNER JOIN [dbo].[PamRotationConfig] C ON C.[Id] = J.[RotationConfigId]
    INNER JOIN [dbo].[PamTargetSystem] T ON T.[Id] = C.[TargetSystemId]
    INNER JOIN [dbo].[PamDaemonTargetAssignment] A ON A.[DaemonId] = @DaemonId AND A.[TargetSystemId] = C.[TargetSystemId]
    INNER JOIN [dbo].[PamDaemon] D ON D.[Id] = @DaemonId AND D.[OrganizationId] = C.[OrganizationId] AND D.[Status] = 0 -- Enabled
    WHERE J.[Status] = 0 -- Pending
        AND J.[NextClaimableAt] <= @Now
        AND C.[Enabled] = 1
        AND T.[Status] = 0 -- Active
END
GO

CREATE OR ALTER PROCEDURE [dbo].[PamRotationJob_Create]
    @Id UNIQUEIDENTIFIER,
    @RotationConfigId UNIQUEIDENTIFIER,
    @Source TINYINT,
    @Status TINYINT,
    @ClaimedByDaemonId UNIQUEIDENTIFIER = NULL,
    @ClaimedAt DATETIME2(7) = NULL,
    @CreationDate DATETIME2(7),
    @NextClaimableAt DATETIME2(7),
    @ExpiresAt DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON
    -- Re-validates eligibility and AtMostOneActiveJobPerConfig before inserting; the transaction holds the range lock until commit.
    SET XACT_ABORT ON

    BEGIN TRANSACTION

    -- Re-checked so a config/target disabled between read and write can't mint a job.
    IF NOT EXISTS (
        SELECT 1
        FROM [dbo].[PamRotationConfig] C WITH (UPDLOCK, HOLDLOCK)
        INNER JOIN [dbo].[PamTargetSystem] T ON T.[Id] = C.[TargetSystemId]
        WHERE C.[Id] = @RotationConfigId
            AND C.[Enabled] = 1
            AND T.[Method] = 0 -- Automatic
            AND T.[Status] = 0 -- Active
    )
    BEGIN
        ROLLBACK TRANSACTION
        SELECT -1 -- ConfigNotOfferable
        RETURN
    END

    -- AtMostOneActiveJobPerConfig: the range lock holds for the transaction, blocking concurrent creation.
    IF EXISTS (
        SELECT 1
        FROM [dbo].[PamRotationJob] WITH (UPDLOCK, HOLDLOCK)
        WHERE [RotationConfigId] = @RotationConfigId
            AND [Status] IN (0, 1) -- Pending, Claimed
    )
    BEGIN
        ROLLBACK TRANSACTION
        SELECT 0 -- ActiveJobExists
        RETURN
    END

    INSERT INTO [dbo].[PamRotationJob]
    (
        [Id], [RotationConfigId], [Source], [Status], [ClaimedByDaemonId], [ClaimedAt],
        [CreationDate], [NextClaimableAt], [ExpiresAt]
    )
    VALUES
    (
        @Id, @RotationConfigId, @Source, @Status, @ClaimedByDaemonId, @ClaimedAt,
        @CreationDate, @NextClaimableAt, @ExpiresAt
    )

    COMMIT TRANSACTION

    SELECT 1 -- Created
END
GO

CREATE OR ALTER PROCEDURE [dbo].[PamRotationJob_Claim]
    @JobId UNIQUEIDENTIFIER,
    @AttemptId UNIQUEIDENTIFIER,
    @DaemonId UNIQUEIDENTIFIER,
    @Now DATETIME2(7),
    @ReleaseDelaySeconds INT
AS
BEGIN
    SET NOCOUNT ON
    -- First-claim-wins: the UPDATE's WHERE Status = 0 takes the row lock, serializing concurrent claims.
    SET XACT_ABORT ON

    BEGIN TRANSACTION

    UPDATE J
    SET J.[Status] = 1, -- Claimed
        J.[ClaimedByDaemonId] = @DaemonId,
        J.[ClaimedAt] = @Now
    FROM [dbo].[PamRotationJob] J
    INNER JOIN [dbo].[PamRotationConfig] C ON C.[Id] = J.[RotationConfigId]
    INNER JOIN [dbo].[PamTargetSystem] T ON T.[Id] = C.[TargetSystemId]
    INNER JOIN [dbo].[PamDaemonTargetAssignment] A ON A.[DaemonId] = @DaemonId AND A.[TargetSystemId] = C.[TargetSystemId]
    -- Defense in depth: re-checks Enabled and org match already checked by the caller's token.
    INNER JOIN [dbo].[PamDaemon] D ON D.[Id] = @DaemonId AND D.[OrganizationId] = C.[OrganizationId] AND D.[Status] = 0 -- Enabled
    WHERE J.[Id] = @JobId
        AND J.[Status] = 0 -- Pending
        AND J.[NextClaimableAt] <= @Now
        AND C.[Enabled] = 1
        AND T.[Status] = 0 -- Active

    IF @@ROWCOUNT = 0
    BEGIN
        -- Unknown job or one outside this daemon's assignment share NotEligible; no existence oracle.
        DECLARE @Outcome INT = CASE
            WHEN NOT EXISTS (
                SELECT 1
                FROM [dbo].[PamRotationJob] J2
                INNER JOIN [dbo].[PamRotationConfig] C2 ON C2.[Id] = J2.[RotationConfigId]
                INNER JOIN [dbo].[PamDaemonTargetAssignment] A2 ON A2.[DaemonId] = @DaemonId AND A2.[TargetSystemId] = C2.[TargetSystemId]
                INNER JOIN [dbo].[PamDaemon] D2 ON D2.[Id] = @DaemonId AND D2.[OrganizationId] = C2.[OrganizationId] AND D2.[Status] = 0 -- Enabled
                WHERE J2.[Id] = @JobId
            ) THEN -1 -- NotEligible (unknown job, or a job outside this daemon's assignment/org)
            ELSE 0 -- NotClaimable: eligible, but not pending / in backoff / held
        END

        ROLLBACK TRANSACTION

        SELECT
            @Outcome AS [Outcome],
            CAST(NULL AS UNIQUEIDENTIFIER) AS [AttemptId],
            CAST(NULL AS UNIQUEIDENTIFIER) AS [JobId],
            CAST(NULL AS TINYINT) AS [Source],
            CAST(NULL AS UNIQUEIDENTIFIER) AS [TargetSystemId],
            CAST(NULL AS NVARCHAR(200)) AS [TargetSystemName],
            CAST(NULL AS TINYINT) AS [Kind],
            CAST(NULL AS NVARCHAR(2000)) AS [PasswordPolicy],
            CAST(NULL AS UNIQUEIDENTIFIER) AS [CipherId],
            CAST(NULL AS NVARCHAR(500)) AS [AccountIdentity],
            CAST(NULL AS BIT) AS [TerminateSessions],
            CAST(NULL AS DATETIME2(7)) AS [ExecuteBy]
        RETURN
    END

    -- AtMostOneInFlightAttemptPerJob: the Executing attempt is created in the same transaction as the claim.
    INSERT INTO [dbo].[PamRotationAttempt]
    (
        [Id], [JobId], [ClaimedByDaemonId], [CipherUpdated], [Status], [FailureReason], [SyncState],
        [SessionTermination], [CreationDate], [ResolvedDate]
    )
    VALUES
    (
        @AttemptId, @JobId, @DaemonId, 0, 0 /* Executing */, NULL, NULL,
        NULL, @Now, NULL
    )

    COMMIT TRANSACTION

    -- ExecuteBy is this claim's lease end (ClaimedAt + ReleaseDelay).
    SELECT
        1 AS [Outcome], -- Claimed
        @AttemptId AS [AttemptId],
        J.[Id] AS [JobId],
        J.[Source],
        T.[Id] AS [TargetSystemId],
        T.[Name] AS [TargetSystemName],
        T.[Kind],
        T.[PasswordPolicy],
        C.[CipherId],
        C.[AccountIdentity],
        C.[TerminateSessions],
        DATEADD(SECOND, @ReleaseDelaySeconds, @Now) AS [ExecuteBy]
    FROM [dbo].[PamRotationJob] J
    INNER JOIN [dbo].[PamRotationConfig] C ON C.[Id] = J.[RotationConfigId]
    INNER JOIN [dbo].[PamTargetSystem] T ON T.[Id] = C.[TargetSystemId]
    WHERE J.[Id] = @JobId
END
GO

CREATE OR ALTER PROCEDURE [dbo].[PamRotationAttempt_AcceptCipherWrite]
    @AttemptId UNIQUEIDENTIFIER,
    @DaemonId UNIQUEIDENTIFIER,
    @CipherData NVARCHAR(MAX),
    @LastKnownRevisionDate DATETIME2(7),
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON
    -- UPDLOCK on the job row closes the check-then-act window before the cipher write.
    SET XACT_ABORT ON

    BEGIN TRANSACTION

    DECLARE @CipherId UNIQUEIDENTIFIER
    DECLARE @OrganizationId UNIQUEIDENTIFIER
    DECLARE @VerifiedJobId UNIQUEIDENTIFIER

    SELECT
        @CipherId = C.[CipherId],
        @OrganizationId = C.[OrganizationId],
        @VerifiedJobId = J.[Id]
    FROM [dbo].[PamRotationAttempt] AT
    INNER JOIN [dbo].[PamRotationJob] J WITH (UPDLOCK) ON J.[Id] = AT.[JobId]
    INNER JOIN [dbo].[PamRotationConfig] C ON C.[Id] = J.[RotationConfigId]
    WHERE AT.[Id] = @AttemptId
        AND AT.[Status] = 0 -- Executing
        AND AT.[ClaimedByDaemonId] = @DaemonId
        AND J.[Status] = 1 -- Claimed
        AND J.[ClaimedByDaemonId] = @DaemonId

    IF @VerifiedJobId IS NULL
    BEGIN
        -- Unknown attempt, wrong claimant, or an already-resolved job; caller audits as write_rejected.
        ROLLBACK TRANSACTION
        SELECT 0 -- Rejected
        RETURN
    END

    -- A drifted LastKnownRevisionDate means a concurrent edit; rejected rather than clobbered.
    IF ABS(DATEDIFF_BIG(MILLISECOND, (SELECT [RevisionDate] FROM [dbo].[Cipher] WHERE [Id] = @CipherId), @LastKnownRevisionDate)) > 1000
    BEGIN
        ROLLBACK TRANSACTION
        SELECT -1 -- RevisionMismatch
        RETURN
    END

    UPDATE [dbo].[Cipher]
    SET [Data] = @CipherData,
        [RevisionDate] = @Now
    WHERE [Id] = @CipherId

    UPDATE [dbo].[PamRotationAttempt]
    SET [CipherUpdated] = 1
    WHERE [Id] = @AttemptId

    -- Other writers of dbo.Cipher bump here too, so clients see the new password.
    EXEC [dbo].[User_BumpAccountRevisionDateByCipherId] @CipherId, @OrganizationId

    COMMIT TRANSACTION

    SELECT 1 -- Accepted
END
GO

CREATE OR ALTER PROCEDURE [dbo].[PamRotationAttempt_MarkRotated]
    @AttemptId UNIQUEIDENTIFIER,
    @DaemonId UNIQUEIDENTIFIER,
    @SessionTermination TINYINT,
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON
    -- CipherUpdated = 1 backstops VerifiedBeforeSuccess so a success report can't resolve an unaccepted write.
    SET XACT_ABORT ON

    BEGIN TRANSACTION

    DECLARE @JobId UNIQUEIDENTIFIER

    SELECT @JobId = J.[Id]
    FROM [dbo].[PamRotationAttempt] AT
    INNER JOIN [dbo].[PamRotationJob] J WITH (UPDLOCK) ON J.[Id] = AT.[JobId]
    WHERE AT.[Id] = @AttemptId
        AND AT.[Status] = 0 -- Executing
        AND AT.[ClaimedByDaemonId] = @DaemonId
        AND AT.[CipherUpdated] = 1
        AND J.[Status] = 1 -- Claimed

    IF @JobId IS NULL
    BEGIN
        ROLLBACK TRANSACTION
        SELECT 0 -- Rejected
        RETURN
    END

    UPDATE [dbo].[PamRotationAttempt]
    SET [Status] = 1, -- Rotated
        [SessionTermination] = @SessionTermination,
        [ResolvedDate] = @Now
    WHERE [Id] = @AttemptId

    -- Clears claim fields leaving Claimed; the attempt already recorded who worked it.
    UPDATE [dbo].[PamRotationJob]
    SET [Status] = 2, -- Succeeded
        [ClaimedByDaemonId] = NULL,
        [ClaimedAt] = NULL
    WHERE [Id] = @JobId

    COMMIT TRANSACTION

    SELECT 1 -- Resolved
END
GO

CREATE OR ALTER PROCEDURE [dbo].[PamRotationAttempt_MarkErrored]
    @AttemptId UNIQUEIDENTIFIER,
    @DaemonId UNIQUEIDENTIFIER,
    @FailureReason NVARCHAR(500) = NULL,
    @SyncState TINYINT,
    @Now DATETIME2(7),
    @MaxAttempts INT,
    @RetryBaseDelaySeconds INT
AS
BEGIN
    SET NOCOUNT ON
    -- @FailureReason is pre-bounded by the caller under the zero-knowledge contract.
    SET XACT_ABORT ON

    BEGIN TRANSACTION

    DECLARE @JobId UNIQUEIDENTIFIER

    SELECT @JobId = J.[Id]
    FROM [dbo].[PamRotationAttempt] AT
    INNER JOIN [dbo].[PamRotationJob] J WITH (UPDLOCK) ON J.[Id] = AT.[JobId]
    WHERE AT.[Id] = @AttemptId
        AND AT.[Status] = 0 -- Executing
        AND AT.[ClaimedByDaemonId] = @DaemonId
        AND J.[Status] = 1 -- Claimed

    IF @JobId IS NULL
    BEGIN
        ROLLBACK TRANSACTION
        SELECT 0 AS [Outcome], NULL AS [JobStatus], NULL AS [ErroredAttemptCount] -- Rejected
        RETURN
    END

    UPDATE [dbo].[PamRotationAttempt]
    SET [Status] = 2, -- Errored
        [FailureReason] = @FailureReason,
        [SyncState] = @SyncState,
        [ResolvedDate] = @Now
    WHERE [Id] = @AttemptId

    -- Only Errored attempts count against the retry budget; Abandoned (released/timed-out) tries never do.
    DECLARE @ErroredCount INT

    SELECT @ErroredCount = COUNT(*)
    FROM [dbo].[PamRotationAttempt]
    WHERE [JobId] = @JobId AND [Status] = 2 -- Errored

    DECLARE @JobStatus TINYINT

    IF @ErroredCount < @MaxAttempts
    BEGIN
        SET @JobStatus = 0 -- Pending
        UPDATE [dbo].[PamRotationJob]
        SET [Status] = @JobStatus,
            [ClaimedByDaemonId] = NULL,
            [ClaimedAt] = NULL,
            [NextClaimableAt] = DATEADD(SECOND, CAST(@RetryBaseDelaySeconds * POWER(2, @ErroredCount - 1) AS INT), @Now)
        WHERE [Id] = @JobId
    END
    ELSE
    BEGIN
        SET @JobStatus = 3 -- Failed
        UPDATE [dbo].[PamRotationJob]
        SET [Status] = @JobStatus,
            [ClaimedByDaemonId] = NULL,
            [ClaimedAt] = NULL
        WHERE [Id] = @JobId
    END

    COMMIT TRANSACTION

    SELECT 1 AS [Outcome], @JobStatus AS [JobStatus], @ErroredCount AS [ErroredAttemptCount] -- Resolved
END
GO

CREATE OR ALTER PROCEDURE [dbo].[PamRotationJob_TimeoutDue]
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON
    -- Success wins: a Rotated attempt excludes the job past ExpiresAt; both commit together.
    SET XACT_ABORT ON

    BEGIN TRANSACTION

    DECLARE @Affected TABLE (
        [JobId] UNIQUEIDENTIFIER NOT NULL,
        [PreviousClaimedByDaemonId] UNIQUEIDENTIFIER NULL
    )

    UPDATE J
    SET J.[Status] = 4, -- TimedOut
        J.[ClaimedByDaemonId] = NULL,
        J.[ClaimedAt] = NULL
    OUTPUT deleted.[Id], deleted.[ClaimedByDaemonId] INTO @Affected ([JobId], [PreviousClaimedByDaemonId])
    FROM [dbo].[PamRotationJob] J
    WHERE J.[Status] IN (0, 1) -- Pending, Claimed
        AND J.[ExpiresAt] <= @Now
        AND NOT EXISTS (
            SELECT 1
            FROM [dbo].[PamRotationAttempt] AT
            WHERE AT.[JobId] = J.[Id] AND AT.[Status] = 1 -- Rotated
        )

    -- Abandons any executing attempt; Abandoned attempts don't count against the retry budget.
    UPDATE [dbo].[PamRotationAttempt]
    SET [Status] = 3, -- Abandoned
        [ResolvedDate] = @Now
    WHERE [JobId] IN (SELECT [JobId] FROM @Affected)
        AND [Status] = 0 -- Executing

    -- One row per timed-out job; AttemptCount tells unroutable (never claimed) from stuck (claimed).
    SELECT
        AF.[JobId],
        C.[Id] AS [RotationConfigId],
        C.[OrganizationId],
        C.[CipherId],
        J.[Source],
        AF.[PreviousClaimedByDaemonId] AS [ClaimedByDaemonId],
        (SELECT COUNT(*) FROM [dbo].[PamRotationAttempt] AT WHERE AT.[JobId] = AF.[JobId]) AS [AttemptCount]
    FROM @Affected AF
    INNER JOIN [dbo].[PamRotationJob] J ON J.[Id] = AF.[JobId]
    INNER JOIN [dbo].[PamRotationConfig] C ON C.[Id] = J.[RotationConfigId]

    COMMIT TRANSACTION
END
GO

CREATE OR ALTER PROCEDURE [dbo].[PamRotationJob_ReleaseExpiredLeases]
    @Now DATETIME2(7),
    @OfflineAfterSeconds INT,
    @ReleaseDelaySeconds INT
AS
BEGIN
    SET NOCOUNT ON
    -- Requires an expired lease and stale heartbeat, not Status alone; excludes Rotated (success wins).
    SET XACT_ABORT ON

    BEGIN TRANSACTION

    DECLARE @Affected TABLE (
        [JobId] UNIQUEIDENTIFIER NOT NULL,
        [PreviousClaimedByDaemonId] UNIQUEIDENTIFIER NULL
    )

    UPDATE J
    SET J.[Status] = 0, -- Pending
        -- Reads ClaimedAt before this UPDATE clears it, so the re-claim time is exactly ExecuteBy.
        J.[NextClaimableAt] = DATEADD(SECOND, @ReleaseDelaySeconds, J.[ClaimedAt]),
        J.[ClaimedByDaemonId] = NULL,
        J.[ClaimedAt] = NULL
    OUTPUT deleted.[Id], deleted.[ClaimedByDaemonId] INTO @Affected ([JobId], [PreviousClaimedByDaemonId])
    FROM [dbo].[PamRotationJob] J
    INNER JOIN [dbo].[PamDaemon] D ON D.[Id] = J.[ClaimedByDaemonId]
    WHERE J.[Status] = 1 -- Claimed
        AND DATEADD(SECOND, @ReleaseDelaySeconds, J.[ClaimedAt]) <= @Now
        AND (D.[LastHeartbeatAt] IS NULL OR D.[LastHeartbeatAt] < DATEADD(SECOND, -@OfflineAfterSeconds, @Now))
        AND NOT EXISTS (
            SELECT 1
            FROM [dbo].[PamRotationAttempt] AT
            WHERE AT.[JobId] = J.[Id] AND AT.[Status] = 1 -- Rotated
        )

    -- Abandoned attempts are never charged against the retry budget.
    UPDATE [dbo].[PamRotationAttempt]
    SET [Status] = 3, -- Abandoned
        [ResolvedDate] = @Now
    WHERE [JobId] IN (SELECT [JobId] FROM @Affected)
        AND [Status] = 0 -- Executing

    -- One row per released job; ClaimedByDaemonId is the pre-clear claimant (always non-null).
    SELECT
        AF.[JobId],
        C.[Id] AS [RotationConfigId],
        C.[OrganizationId],
        C.[CipherId],
        J.[Source],
        AF.[PreviousClaimedByDaemonId] AS [ClaimedByDaemonId]
    FROM @Affected AF
    INNER JOIN [dbo].[PamRotationJob] J ON J.[Id] = AF.[JobId]
    INNER JOIN [dbo].[PamRotationConfig] C ON C.[Id] = J.[RotationConfigId]

    COMMIT TRANSACTION
END
GO

CREATE OR ALTER PROCEDURE [dbo].[AccessLease_ExpireDue]
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    -- Expiry is derived, not stored; UPDLOCK/HOLDLOCK decides which sweep run owns a lease.
    DECLARE @Due TABLE ([AccessLeaseId] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY);

    INSERT INTO [dbo].[PamLeaseExpirySweep] ([AccessLeaseId], [SweptDate])
    OUTPUT inserted.[AccessLeaseId] INTO @Due
    SELECT
        AL.[Id],
        @Now
    FROM [dbo].[AccessLease] AL
    WHERE AL.[Action] = 0 -- None: no early end recorded, so the closed window is a natural expiry
        AND AL.[NotAfter] <= @Now
        AND NOT EXISTS (
            SELECT 1
            FROM [dbo].[PamLeaseExpirySweep] S WITH (UPDLOCK, HOLDLOCK)
            WHERE S.[AccessLeaseId] = AL.[Id]
        )

    -- No join needed; everything the caller audits lives on the lease row.
    SELECT
        AL.[Id],
        AL.[OrganizationId],
        AL.[CollectionId],
        AL.[CipherId],
        AL.[RequesterId],
        AL.[NotBefore],
        AL.[NotAfter]
    FROM [dbo].[AccessLease] AL
    INNER JOIN @Due D ON D.[AccessLeaseId] = AL.[Id]
END
GO

-- Rotation columns snapshot TargetSystemName/DaemonName at write, like RuleName, rather than JOIN.

IF COL_LENGTH('[dbo].[AccessAuditEvent]', 'TargetSystemId') IS NULL
BEGIN
    ALTER TABLE [dbo].[AccessAuditEvent] ADD
        [TargetSystemId]    UNIQUEIDENTIFIER    NULL,
        [TargetSystemName]  NVARCHAR(200)       NULL,
        [DaemonId]          UNIQUEIDENTIFIER    NULL,
        [DaemonName]        NVARCHAR(200)       NULL,
        [RotationConfigId]  UNIQUEIDENTIFIER    NULL,
        [RotationJobId]     UNIQUEIDENTIFIER    NULL,
        [RotationSource]    TINYINT             NULL,
        [SyncState]         TINYINT             NULL;
END
GO

CREATE OR ALTER PROCEDURE [dbo].[AccessAuditEvent_Create]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @CorrelationId UNIQUEIDENTIFIER,
    @Kind TINYINT,
    @Phase TINYINT,
    @OccurredAt DATETIME2(7),
    @ActorId UNIQUEIDENTIFIER = NULL,
    @RequesterId UNIQUEIDENTIFIER = NULL,
    @CollectionId UNIQUEIDENTIFIER = NULL,
    @CipherId UNIQUEIDENTIFIER = NULL,
    @AccessRequestId UNIQUEIDENTIFIER = NULL,
    @AccessLeaseId UNIQUEIDENTIFIER = NULL,
    @AccessRuleId UNIQUEIDENTIFIER = NULL,
    @RuleName NVARCHAR(256) = NULL,
    @Detail NVARCHAR(MAX) = NULL,
    @LeaseNotBefore DATETIME2(7) = NULL,
    @LeaseNotAfter DATETIME2(7) = NULL,
    @TargetSystemId UNIQUEIDENTIFIER = NULL,
    @TargetSystemName NVARCHAR(200) = NULL,
    @DaemonId UNIQUEIDENTIFIER = NULL,
    @DaemonName NVARCHAR(200) = NULL,
    @RotationConfigId UNIQUEIDENTIFIER = NULL,
    @RotationJobId UNIQUEIDENTIFIER = NULL,
    @RotationSource TINYINT = NULL,
    @SyncState TINYINT = NULL
AS
BEGIN
    SET NOCOUNT ON

    -- Names are caller-supplied, snapshotted at write, not JOINed since source rows can be deleted.
    INSERT INTO [dbo].[AccessAuditEvent]
    (
        [Id],
        [OrganizationId],
        [CorrelationId],
        [Kind],
        [Phase],
        [OccurredAt],
        [ActorId],
        [RequesterId],
        [CollectionId],
        [CipherId],
        [AccessRequestId],
        [AccessLeaseId],
        [AccessRuleId],
        [Detail],
        [LeaseNotBefore],
        [LeaseNotAfter],
        [ActorName],
        [ActorEmail],
        [RequesterName],
        [RequesterEmail],
        [CipherName],
        [CollectionName],
        [RuleName],
        [TargetSystemId],
        [TargetSystemName],
        [DaemonId],
        [DaemonName],
        [RotationConfigId],
        [RotationJobId],
        [RotationSource],
        [SyncState]
    )
    SELECT
        @Id,
        @OrganizationId,
        @CorrelationId,
        @Kind,
        @Phase,
        @OccurredAt,
        @ActorId,
        @RequesterId,
        @CollectionId,
        @CipherId,
        @AccessRequestId,
        @AccessLeaseId,
        @AccessRuleId,
        @Detail,
        @LeaseNotBefore,
        @LeaseNotAfter,
        AU.[Name],
        AU.[Email],
        RU.[Name],
        RU.[Email],
        JSON_VALUE(C.[Data], '$.Name'),
        COL.[Name],
        @RuleName,
        @TargetSystemId,
        @TargetSystemName,
        @DaemonId,
        @DaemonName,
        @RotationConfigId,
        @RotationJobId,
        @RotationSource,
        @SyncState
    FROM (SELECT 1 AS [X]) Seed
    LEFT JOIN [dbo].[User] AU ON AU.[Id] = @ActorId
    LEFT JOIN [dbo].[User] RU ON RU.[Id] = @RequesterId
    LEFT JOIN [dbo].[Cipher] C ON C.[Id] = @CipherId
    LEFT JOIN [dbo].[Collection] COL ON COL.[Id] = @CollectionId
END
GO

CREATE OR ALTER PROCEDURE [dbo].[AccessAuditEvent_ReadManyByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER,
    @Since DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    -- Names are frozen at write time; a later delete or rename can't alter history.
    SELECT
        [Kind],
        [Phase],
        [CorrelationId],
        [OccurredAt],
        [OrganizationId],
        [ActorId],
        [RequesterId],
        [CollectionId],
        [CipherId],
        [AccessRequestId],
        [AccessLeaseId],
        [AccessRuleId],
        [Detail],
        [LeaseNotBefore],
        [LeaseNotAfter],
        [ActorName],
        [ActorEmail],
        [RequesterName],
        [RequesterEmail],
        [CipherName],
        [CollectionName],
        [RuleName],
        [TargetSystemId],
        [TargetSystemName],
        [DaemonId],
        [DaemonName],
        [RotationConfigId],
        [RotationJobId],
        [RotationSource],
        [SyncState]
    FROM [dbo].[AccessAuditEvent]
    WHERE [OrganizationId] = @OrganizationId
        AND [OccurredAt] >= @Since
    ORDER BY [OccurredAt] DESC
END
GO

-- Added for Repository<T,TId>.GetByIdAsync; ApiKey had no _ReadById.

CREATE OR ALTER PROCEDURE [dbo].[ApiKey_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[ApiKeyView]
    WHERE
        [Id] = @Id
END
GO

-- Backs the daemon detail page's recent-activity read (GET .../rotation/daemons/{id}).

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'IX_PamRotationAttempt_ClaimedByDaemonId_JobId' AND [object_id] = OBJECT_ID('[dbo].[PamRotationAttempt]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_PamRotationAttempt_ClaimedByDaemonId_JobId]
        ON [dbo].[PamRotationAttempt] ([ClaimedByDaemonId] ASC, [JobId] ASC);
END
GO

CREATE OR ALTER PROCEDURE [dbo].[PamRotationJob_ReadManyRecentByDaemonId]
    @DaemonId UNIQUEIDENTIFIER,
    @Limit INT
AS
BEGIN
    SET NOCOUNT ON

    -- Membership is by PamRotationAttempt.ClaimedByDaemonId, not the job's own field, cleared after resolution.
    SELECT TOP (@Limit) J.*
    INTO #Jobs
    FROM [dbo].[PamRotationJob] J
    WHERE EXISTS (
        SELECT 1
        FROM [dbo].[PamRotationAttempt] A
        WHERE A.[JobId] = J.[Id]
            AND A.[ClaimedByDaemonId] = @DaemonId
    )
    ORDER BY J.[CreationDate] DESC

    SELECT *
    FROM #Jobs
    ORDER BY [CreationDate] DESC

    SELECT A.*
    FROM [dbo].[PamRotationAttempt] A
    INNER JOIN #Jobs J ON J.[Id] = A.[JobId]
    WHERE A.[ClaimedByDaemonId] = @DaemonId
    ORDER BY A.[JobId], A.[CreationDate] ASC

    DROP TABLE #Jobs
END
GO

-- Daemon hard-delete, invoked by the generic Repository<PamDaemon, Guid>.DeleteAsync convention.

CREATE OR ALTER PROCEDURE [dbo].[PamDaemon_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON
    -- Delete order: assignments, then daemon, then ApiKey (both FKs are NO ACTION).
    SET XACT_ABORT ON

    DECLARE @Now DATETIME2(7) = GETUTCDATE()
    DECLARE @ApiKeyId UNIQUEIDENTIFIER

    BEGIN TRANSACTION

    -- The stored row decides which credential goes; the caller's ApiKeyId is not trusted.
    SELECT @ApiKeyId = [ApiKeyId]
    FROM [dbo].[PamDaemon]
    WHERE [Id] = @Id

    -- No FK ties PamRotationJob to PamDaemon, so this clears the daemon's claimed jobs directly.
    UPDATE AT
    SET AT.[Status] = 3, -- Abandoned
        AT.[ResolvedDate] = @Now
    FROM [dbo].[PamRotationAttempt] AT
    INNER JOIN [dbo].[PamRotationJob] J ON J.[Id] = AT.[JobId]
    WHERE AT.[Status] = 0 -- Executing
        AND J.[ClaimedByDaemonId] = @Id
        AND J.[Status] = 1 -- Claimed

    UPDATE [dbo].[PamRotationJob]
    SET [Status] = 0, -- Pending
        [ClaimedByDaemonId] = NULL,
        [ClaimedAt] = NULL,
        [NextClaimableAt] = @Now
    WHERE [ClaimedByDaemonId] = @Id
        AND [Status] = 1 -- Claimed

    DELETE FROM [dbo].[PamDaemonTargetAssignment]
    WHERE [DaemonId] = @Id

    DELETE FROM [dbo].[PamDaemon]
    WHERE [Id] = @Id

    DELETE FROM [dbo].[ApiKey]
    WHERE [Id] = @ApiKeyId

    COMMIT TRANSACTION
END
GO
