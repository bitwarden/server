-- PAM Credential Rotation: PamTargetSystem / PamDaemon / PamDaemonTargetAssignment / PamRotationConfig /
-- PamRotationJob / PamRotationAttempt tables + procedures, plus the AccessLease natural-expiry sweep procedure
-- (AccessLease itself is untouched -- see src/Sql/dbo/Pam/Tables/AccessLease.sql).

-- PamTargetSystem
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

-- PamDaemon
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

-- PamDaemonTargetAssignment
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

    -- OneAssignmentPerDaemonTarget.
    CREATE UNIQUE NONCLUSTERED INDEX [IX_PamDaemonTargetAssignment_DaemonId_TargetSystemId]
        ON [dbo].[PamDaemonTargetAssignment] ([DaemonId] ASC, [TargetSystemId] ASC);

    CREATE NONCLUSTERED INDEX [IX_PamDaemonTargetAssignment_TargetSystemId]
        ON [dbo].[PamDaemonTargetAssignment] ([TargetSystemId] ASC);
END
GO

-- PamRotationConfig
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

-- PamRotationJob
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

-- PamRotationAttempt
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

    -- No cascade cleanup here: a target system with rotation configs or daemon assignments still referencing it is
    -- blocked by their NO ACTION FKs (detach or delete those first). Deleting an already-gone row is a no-op.
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

    -- Name + Status only: ApiKeyId is set once at registration (reissue is deferred -- see the plan's deferrals),
    -- OrganizationId/CreationDate never change, and LastHeartbeatAt has its own conditional-bump sproc
    -- (PamDaemon_UpdateHeartbeat) so a routine admin edit never races a daemon's own poll. The repository must call
    -- this with an explicit narrow parameter set rather than the generic whole-entity Update -- passing the full
    -- PamDaemon entity here would fail (this sproc does not declare an ApiKeyId/LastHeartbeatAt/etc. parameter).
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

    -- Conditional bump: the daemon-facing request filter calls this on every request, so the WHERE guard turns
    -- most calls into a no-op write instead of hammering the row -- only a poll arriving after @MinIntervalSeconds
    -- since the last recorded heartbeat actually updates it. Never called by a sweep -- only by the daemon's own
    -- requests.
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

    -- The client provider's lookup at token-issuance time: the daemon row plus the two organization flags that gate
    -- issuance (Enabled, UsePam) so a lapsed/disabled org's daemon cannot mint a token without an extra round trip.
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

    -- @Id is a plain input, not OUTPUT: unlike the generic Create sprocs, the caller (IPamDaemonRepository.
    -- CreateAssignmentAsync) always assigns the id before calling this. [IX_PamDaemonTargetAssignment_DaemonId_TargetSystemId]
    -- is the unique-index backstop for OneAssignmentPerDaemonTarget if two callers race.
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

    -- The config detail page's header projection (IPamRotationConfigRepository.GetDetailsByIdAsync): the target's
    -- display name/method denormalized, plus a computed HasActiveJob so the caller can gate Delete/UpdateAccount
    -- without a second round trip. "Active" mirrors PamRotationJob_Create's guard: Pending or Claimed.
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

    -- The schedule-list view: every config for the org, with the target's display name/method denormalized (so the
    -- client avoids an N+1) and a computed HasActiveJob so the UI can gate Delete/UpdateAccount without a second
    -- round trip. "Active" mirrors PamRotationJob_Create's guard: Pending or Claimed.
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

    -- The sweep's due phase (spec RotationDue): enabled, automatic, active-target configs whose schedule has come
    -- due, with no job already in flight (OfferRotation is the single creation point -- this feeds it, one
    -- OfferRotationCommand call per row). Enabled + NextRotationAt IS NOT NULL matches
    -- [IX_PamRotationConfig_NextRotationAt] so the scan is a narrow range seek, not a table scan.
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
    -- DeleteRotationConfigCommand's cascade: the audit trail (AccessAuditEvent) is the durable history of a config's
    -- rotations, so jobs/attempts are hard-deleted here rather than soft-retired. Order matters -- attempts reference
    -- jobs, jobs reference the config, and both FKs are ON DELETE NO ACTION -- so children must go first. The caller
    -- has already confirmed the config has no active job.
    SET XACT_ABORT ON

    BEGIN TRANSACTION

    DELETE A
    FROM [dbo].[PamRotationAttempt] A
    INNER JOIN [dbo].[PamRotationJob] J ON J.[Id] = A.[JobId]
    WHERE J.[RotationConfigId] = @Id

    DELETE FROM [dbo].[PamRotationJob]
    WHERE [RotationConfigId] = @Id

    DELETE FROM [dbo].[PamRotationConfig]
    WHERE [Id] = @Id

    COMMIT TRANSACTION
END
GO

CREATE OR ALTER PROCEDURE [dbo].[PamRotationConfig_AnyByTargetSystemWithTerminateSessions]
    @TargetSystemId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    -- UpdateTargetSystemPolicyCommand's capability-withdrawal guard: SupportsSessionTermination may only be turned
    -- off when no config on the target still opts into TerminateSessions.
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

    -- The config detail page's attempt history, returned as two result sets so the caller can zip each job to its
    -- attempts (IPamRotationJobRepository.GetManyByConfigIdAsync, grouping the second set by JobId) without an N+1:
    --   1) every job for the config, newest first.
    --   2) every attempt belonging to those jobs, oldest-first within a job.
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

    -- The daemon poll: jobs this daemon may claim right now. Re-derives every eligibility condition
    -- PamRotationJob_Claim itself re-checks (config enabled, target active, an assignment exists, and -- defense in
    -- depth -- the daemon's own org matches the config's org) so the list a daemon sees and what it can actually
    -- claim never diverge.
    SELECT J.*
    FROM [dbo].[PamRotationJob] J
    INNER JOIN [dbo].[PamRotationConfig] C ON C.[Id] = J.[RotationConfigId]
    INNER JOIN [dbo].[PamTargetSystem] T ON T.[Id] = C.[TargetSystemId]
    INNER JOIN [dbo].[PamDaemonTargetAssignment] A ON A.[DaemonId] = @DaemonId AND A.[TargetSystemId] = C.[TargetSystemId]
    INNER JOIN [dbo].[PamDaemon] D ON D.[Id] = @DaemonId AND D.[OrganizationId] = C.[OrganizationId]
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
    -- IPamRotationJobRepository.CreateGuardedAsync passes an already fully-populated PamRotationJob (Status =
    -- Pending, claim fields null, NextClaimableAt/ExpiresAt already computed by the caller) -- this sproc only
    -- re-validates can_offer's eligibility half and the AtMostOneActiveJobPerConfig guard before inserting it as-is
    -- (spec OfferRotation's single creation point). An explicit transaction is required so the range lock below is
    -- held until the INSERT commits; XACT_ABORT guarantees rollback (and a clean pooled connection) on any error.
    SET XACT_ABORT ON

    BEGIN TRANSACTION

    -- can_offer's eligibility half, re-checked here (not just by the caller) so a config disabled or a target
    -- disabled/switched to Manual between the caller's read and this write cannot mint a job. Outcome -1
    -- (ConfigNotOfferable) is distinct from the active-job conflict (0, ActiveJobExists) so the caller can tell
    -- "not offerable" apart from "already has one".
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

    -- AtMostOneActiveJobPerConfig. The UPDLOCK, HOLDLOCK range lock on [IX_PamRotationJob_RotationConfigId_Status] is
    -- held for the life of this transaction, so a concurrent creation attempt for the same config blocks here until
    -- this transaction commits, then sees the new job and is rejected.
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
    -- First-claim-wins is enforced by the UPDATE's own WHERE J.Status = 0 clause: SQL Server takes the row lock
    -- needed to satisfy that predicate as part of the UPDATE itself, so two concurrent claims of the same job
    -- serialize on the row and only the first can flip Status Pending -> Claimed. The result shape mirrors
    -- PamRotationClaimResult exactly (an Outcome column plus the work-snapshot columns, null on any non-Claimed
    -- outcome) so the caller can map every path with a single row read. XACT_ABORT guarantees rollback (and a clean
    -- pooled connection) on any error.
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
    -- Defense in depth: the daemon must be Enrolled AND in the same org as the config, even though the caller
    -- (ClaimRotationJobCommand) already checked both from the bearer token's claims.
    INNER JOIN [dbo].[PamDaemon] D ON D.[Id] = @DaemonId AND D.[OrganizationId] = C.[OrganizationId] AND D.[Status] = 0 -- Enrolled
    WHERE J.[Id] = @JobId
        AND J.[Status] = 0 -- Pending
        AND J.[NextClaimableAt] <= @Now
        AND C.[Enabled] = 1
        AND T.[Status] = 0 -- Active

    IF @@ROWCOUNT = 0
    BEGIN
        -- Eligibility is classified FIRST so a job that does not exist and a job this daemon may not claim
        -- (unassigned target, cross-org, revoked daemon) produce the same NotEligible outcome -- the caller maps it
        -- to 404, leaving no existence oracle. Only an eligible daemon that lost the race / hit backoff / hit the
        -- paused-config or disabled-target hold gets NotClaimable (mapped to 409).
        DECLARE @Outcome INT = CASE
            WHEN NOT EXISTS (
                SELECT 1
                FROM [dbo].[PamRotationJob] J2
                INNER JOIN [dbo].[PamRotationConfig] C2 ON C2.[Id] = J2.[RotationConfigId]
                INNER JOIN [dbo].[PamDaemonTargetAssignment] A2 ON A2.[DaemonId] = @DaemonId AND A2.[TargetSystemId] = C2.[TargetSystemId]
                INNER JOIN [dbo].[PamDaemon] D2 ON D2.[Id] = @DaemonId AND D2.[OrganizationId] = C2.[OrganizationId] AND D2.[Status] = 0 -- Enrolled
                WHERE J2.[Id] = @JobId
            ) THEN -1 -- NotEligible (unknown job, or a job outside this daemon's assignment/org)
            ELSE 0 -- NotClaimable (eligible, but not pending / in backoff / held by a paused config or disabled target)
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

    -- AtMostOneInFlightAttemptPerJob: the Executing attempt is created in the same transaction as the claim, so a
    -- claimed job always has exactly one in-flight attempt from the moment it is claimed.
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

    -- The work snapshot the daemon executes against; ExecuteBy is this claim's lease end (ClaimedAt + ReleaseDelay).
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
    -- AcceptCipherUpdate's atomic write-capability check (security finding, plan §1): the job row is locked here
    -- WITH (UPDLOCK) for the life of the transaction, so a concurrent release/timeout sweep -- which updates the same
    -- job row -- blocks until this commits (or vice versa), closing the check-then-act window between "is this
    -- attempt still allowed to write" and "write the cipher". XACT_ABORT guarantees rollback (and a clean pooled
    -- connection) on any error.
    SET XACT_ABORT ON

    BEGIN TRANSACTION

    DECLARE @CipherId UNIQUEIDENTIFIER
    DECLARE @VerifiedJobId UNIQUEIDENTIFIER

    SELECT
        @CipherId = C.[CipherId],
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
        -- The complement of spec AcceptCipherUpdate: unknown attempt, wrong claimant, or the job/attempt has already
        -- moved on (released/timed out/resolved). Audited by the caller as write_rejected.
        ROLLBACK TRANSACTION
        SELECT 0 -- Rejected
        RETURN
    END

    -- Outside RejectCipherUpdate's exact complement (plan §10 divergence): a drifted LastKnownRevisionDate means the
    -- vault item changed since the daemon last read it, so the write is rejected to protect a concurrent user edit
    -- rather than silently clobbering it. The 1-second tolerance mirrors CipherService's own last-known-revision
    -- check.
    IF ABS(DATEDIFF(MILLISECOND, (SELECT [RevisionDate] FROM [dbo].[Cipher] WHERE [Id] = @CipherId), @LastKnownRevisionDate)) > 1000
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
    -- RecordRotationSucceeded -> MarkJobSucceeded. CipherUpdated = 1 is the VerifiedBeforeSuccess backstop: a success
    -- report cannot resolve an attempt whose cipher write was never accepted. Guard failure (unknown/stale attempt,
    -- wrong claimant, no cipher write, or the job already moved on) takes the RejectStaleSuccess path -- the caller
    -- audits report_rejected, nothing changes. XACT_ABORT guarantees rollback (and a clean pooled connection) on any
    -- error.
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

    -- Every transition out of Claimed nulls the claim fields; the executing daemon's identity for this try is
    -- already permanently recorded on the attempt above.
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
    -- RecordRotationFailed -> RetryJob / FailJob. @FailureReason is already bounded/truncated by the caller before
    -- this call (the zero-knowledge failure-reason contract forbids forwarding raw target-system error output), so
    -- this sproc only stores it. Guard failure (unknown/stale attempt, wrong claimant, or the job already moved on)
    -- takes the RejectStaleFailureReport path -- the caller audits report_rejected, nothing changes. The result shape
    -- mirrors PamRotationFailureResult (Outcome + JobStatus + ErroredAttemptCount) on every path, success or not.
    -- XACT_ABORT guarantees rollback (and a clean pooled connection) on any error.
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

    -- Retry-budget math: only Errored attempts count (Abandoned -- released/timed-out tries -- are never charged
    -- against the budget, per the plan's success-wins-on-timeout+release semantics).
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
    -- JobTimesOut ("success wins"): a job with a Rotated attempt is excluded even if it is otherwise past ExpiresAt --
    -- a slow-but-successful report still wins the race against the timeout sweep. OUTPUT can't reach through the
    -- joins needed for the audit projection (config/org/cipher), so affected ids are captured in @Affected first and
    -- joined afterward. The job update and its attempt's Abandoned transition commit together so a crash between the
    -- two can never leave a stale Executing attempt behind a job that already moved on. XACT_ABORT guarantees
    -- rollback (and a clean pooled connection) on any error.
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

    -- Abandon the executing attempt (if any) on each timed-out job; a Pending job that never got claimed has none.
    -- Abandoned attempts are never charged against the retry budget.
    UPDATE [dbo].[PamRotationAttempt]
    SET [Status] = 3, -- Abandoned
        [ResolvedDate] = @Now
    WHERE [JobId] IN (SELECT [JobId] FROM @Affected)
        AND [Status] = 0 -- Executing

    -- One row per timed-out job for audit emission; AttemptCount distinguishes unroutable (never claimed, zero
    -- attempts) from stuck (claimed at least once).
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
    -- DaemonConnectionDropsReleaseJobs -> ReleaseJob -> AbandonAttempt, with the lease-respecting timing from plan §5:
    -- release only fires once BOTH the claim's lease has expired (now >= ExecuteBy, i.e. ClaimedAt + ReleaseDelay) AND
    -- the claimant's heartbeat is stale -- never on daemon Status alone, so a revoked daemon's jobs release too once
    -- its heartbeats actually stop. A job with a Rotated attempt is excluded ("success wins", same as the timeout
    -- sweep): a slow-but-live daemon whose report lands inside its lease still wins. OUTPUT can't reach through the
    -- joins needed for the audit projection, so affected ids are captured in @Affected first and joined afterward.
    -- XACT_ABORT guarantees rollback (and a clean pooled connection) on any error.
    SET XACT_ABORT ON

    BEGIN TRANSACTION

    DECLARE @Affected TABLE (
        [JobId] UNIQUEIDENTIFIER NOT NULL,
        [PreviousClaimedByDaemonId] UNIQUEIDENTIFIER NULL
    )

    UPDATE J
    SET J.[Status] = 0, -- Pending
        -- Computed from the pre-clear ClaimedAt (this UPDATE's FROM/JOIN still sees the old value here), so the
        -- re-claim time is exactly ExecuteBy regardless of whether release fires at that instant or later.
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

    -- One row per released job for audit emission. ClaimedByDaemonId here is the pre-clear claimant (always
    -- non-null: only Claimed jobs are released).
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

    -- The anticipated lease natural-expiry sweep (plan decision 4): flips Active -> Expired for leases whose window
    -- closed on its own (no revoke/cancel involved), so the deferred LeaseExpired audit kind and the rotation
    -- access-end trigger both have something to fire from. [IX_AccessLease_NotAfter_Status] makes this a narrow
    -- range seek. No join is needed for the projection -- every column the caller audits/triggers on already lives
    -- on the row itself.
    UPDATE [dbo].[AccessLease]
    SET [Status] = 1 -- Expired
    OUTPUT
        deleted.[Id],
        deleted.[OrganizationId],
        deleted.[CollectionId],
        deleted.[CipherId],
        deleted.[RequesterId],
        deleted.[NotBefore],
        deleted.[NotAfter]
    WHERE [Status] = 0 -- Active
        AND [NotAfter] <= @Now
END
GO

