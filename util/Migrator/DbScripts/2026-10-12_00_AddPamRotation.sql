-- Add PAM credential rotation and the lease expiry sweep. Consolidated net-new migration (the feature has not shipped),
-- squashing the incremental steps rotation went through during development. See the src/Sql/dbo/Pam tables for the
-- schema rationale.

IF OBJECT_ID('[dbo].[PamTargetSystem]') IS NULL
BEGIN
    CREATE TABLE [dbo].[PamTargetSystem] (
        [Id]                            UNIQUEIDENTIFIER    NOT NULL,
        [OrganizationId]                UNIQUEIDENTIFIER    NOT NULL,
        [Name]                          NVARCHAR(200)        NOT NULL,
        [Method]                        TINYINT              NOT NULL,
        [Status]                        TINYINT              NOT NULL,
        [CreationDate]                  DATETIME2(7)         NOT NULL,
        [RevisionDate]                  DATETIME2(7)         NOT NULL,
        [SealedData]                    NVARCHAR(MAX)        NULL,
        CONSTRAINT [PK_PamTargetSystem] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_PamTargetSystem_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id]) ON DELETE CASCADE
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'IX_PamTargetSystem_OrganizationId' AND object_id = OBJECT_ID('[dbo].[PamTargetSystem]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_PamTargetSystem_OrganizationId]
        ON [dbo].[PamTargetSystem] ([OrganizationId] ASC);
END
GO

IF OBJECT_ID('[dbo].[PamAccessConnector]') IS NULL
BEGIN
    CREATE TABLE [dbo].[PamAccessConnector] (
        [Id]                UNIQUEIDENTIFIER    NOT NULL,
        [OrganizationId]    UNIQUEIDENTIFIER    NOT NULL,
        [Name]              NVARCHAR(200)       NOT NULL,
        [ApiKeyId]          UNIQUEIDENTIFIER    NOT NULL,
        [Status]            TINYINT             NOT NULL,
        [LastHeartbeatAt]   DATETIME2(7)        NULL,
        [CreationDate]      DATETIME2(7)        NOT NULL,
        [RevisionDate]      DATETIME2(7)        NOT NULL,
        CONSTRAINT [PK_PamAccessConnector] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_PamAccessConnector_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_PamAccessConnector_ApiKey] FOREIGN KEY ([ApiKeyId]) REFERENCES [dbo].[ApiKey] ([Id]) ON DELETE NO ACTION
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'IX_PamAccessConnector_ApiKeyId' AND object_id = OBJECT_ID('[dbo].[PamAccessConnector]'))
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [IX_PamAccessConnector_ApiKeyId]
        ON [dbo].[PamAccessConnector] ([ApiKeyId] ASC);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'IX_PamAccessConnector_OrganizationId' AND object_id = OBJECT_ID('[dbo].[PamAccessConnector]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_PamAccessConnector_OrganizationId]
        ON [dbo].[PamAccessConnector] ([OrganizationId] ASC);
END
GO

IF OBJECT_ID('[dbo].[PamAccessConnectorTargetAssignment]') IS NULL
BEGIN
    CREATE TABLE [dbo].[PamAccessConnectorTargetAssignment] (
        [Id]                UNIQUEIDENTIFIER    NOT NULL,
        [AccessConnectorId] UNIQUEIDENTIFIER    NOT NULL,
        [TargetSystemId]    UNIQUEIDENTIFIER    NOT NULL,
        [OrganizationId]    UNIQUEIDENTIFIER    NOT NULL,
        [CreationDate]      DATETIME2(7)        NOT NULL,
        CONSTRAINT [PK_PamAccessConnectorTargetAssignment] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_PamAccessConnectorTargetAssignment_AccessConnector] FOREIGN KEY ([AccessConnectorId]) REFERENCES [dbo].[PamAccessConnector] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PamAccessConnectorTargetAssignment_TargetSystem] FOREIGN KEY ([TargetSystemId]) REFERENCES [dbo].[PamTargetSystem] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PamAccessConnectorTargetAssignment_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id]) ON DELETE CASCADE
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'IX_PamAccessConnectorTargetAssignment_AccessConnectorId_TargetSystemId' AND object_id = OBJECT_ID('[dbo].[PamAccessConnectorTargetAssignment]'))
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [IX_PamAccessConnectorTargetAssignment_AccessConnectorId_TargetSystemId]
        ON [dbo].[PamAccessConnectorTargetAssignment] ([AccessConnectorId] ASC, [TargetSystemId] ASC);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'IX_PamAccessConnectorTargetAssignment_TargetSystemId' AND object_id = OBJECT_ID('[dbo].[PamAccessConnectorTargetAssignment]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_PamAccessConnectorTargetAssignment_TargetSystemId]
        ON [dbo].[PamAccessConnectorTargetAssignment] ([TargetSystemId] ASC);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'IX_PamAccessConnectorTargetAssignment_OrganizationId' AND object_id = OBJECT_ID('[dbo].[PamAccessConnectorTargetAssignment]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_PamAccessConnectorTargetAssignment_OrganizationId]
        ON [dbo].[PamAccessConnectorTargetAssignment] ([OrganizationId] ASC);
END
GO

IF OBJECT_ID('[dbo].[PamRotationConfig]') IS NULL
BEGIN
    CREATE TABLE [dbo].[PamRotationConfig] (
        [Id]                    UNIQUEIDENTIFIER    NOT NULL,
        [OrganizationId]        UNIQUEIDENTIFIER    NOT NULL,
        [CipherId]              UNIQUEIDENTIFIER    NOT NULL,
        [TargetSystemId]        UNIQUEIDENTIFIER    NOT NULL,
        [ScheduleCron]          NVARCHAR(100)       NULL,
        [RotateOnAccessEnd]     BIT                 NOT NULL,
        [NextRotationAt]        DATETIME2(7)        NULL,
        [Enabled]               BIT                 NOT NULL,
        [LastRotationAt]        DATETIME2(7)        NULL,
        [CreationDate]          DATETIME2(7)        NOT NULL,
        [RevisionDate]          DATETIME2(7)        NOT NULL,
        [SealedData]            NVARCHAR(MAX)       NOT NULL,
        CONSTRAINT [PK_PamRotationConfig] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_PamRotationConfig_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_PamRotationConfig_TargetSystem] FOREIGN KEY ([TargetSystemId]) REFERENCES [dbo].[PamTargetSystem] ([Id]) ON DELETE NO ACTION
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'IX_PamRotationConfig_CipherId' AND object_id = OBJECT_ID('[dbo].[PamRotationConfig]'))
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [IX_PamRotationConfig_CipherId]
        ON [dbo].[PamRotationConfig] ([CipherId] ASC);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'IX_PamRotationConfig_NextRotationAt' AND object_id = OBJECT_ID('[dbo].[PamRotationConfig]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_PamRotationConfig_NextRotationAt]
        ON [dbo].[PamRotationConfig] ([NextRotationAt] ASC)
        WHERE [Enabled] = 1 AND [NextRotationAt] IS NOT NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'IX_PamRotationConfig_OrganizationId' AND object_id = OBJECT_ID('[dbo].[PamRotationConfig]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_PamRotationConfig_OrganizationId]
        ON [dbo].[PamRotationConfig] ([OrganizationId] ASC);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'IX_PamRotationConfig_TargetSystemId' AND object_id = OBJECT_ID('[dbo].[PamRotationConfig]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_PamRotationConfig_TargetSystemId]
        ON [dbo].[PamRotationConfig] ([TargetSystemId] ASC);
END
GO

IF OBJECT_ID('[dbo].[PamRotationJob]') IS NULL
BEGIN
    CREATE TABLE [dbo].[PamRotationJob] (
        [Id]                            UNIQUEIDENTIFIER    NOT NULL,
        [RotationConfigId]              UNIQUEIDENTIFIER    NOT NULL,
        [Source]                        TINYINT             NOT NULL,
        [Action]                        TINYINT             NOT NULL,
        [ClaimedByAccessConnectorId]    UNIQUEIDENTIFIER    NULL,
        [ClaimedAt]                     DATETIME2(7)        NULL,
        [CreationDate]                  DATETIME2(7)        NOT NULL,
        [NextClaimableAt]               DATETIME2(7)        NOT NULL,
        [ExpiresAt]                     DATETIME2(7)        NOT NULL,
        CONSTRAINT [PK_PamRotationJob] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_PamRotationJob_RotationConfig] FOREIGN KEY ([RotationConfigId]) REFERENCES [dbo].[PamRotationConfig] ([Id]) ON DELETE CASCADE
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'IX_PamRotationJob_RotationConfigId_Action' AND object_id = OBJECT_ID('[dbo].[PamRotationJob]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_PamRotationJob_RotationConfigId_Action]
        ON [dbo].[PamRotationJob] ([RotationConfigId] ASC, [Action] ASC);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'IX_PamRotationJob_Action_ExpiresAt' AND object_id = OBJECT_ID('[dbo].[PamRotationJob]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_PamRotationJob_Action_ExpiresAt]
        ON [dbo].[PamRotationJob] ([Action] ASC, [ExpiresAt] ASC);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'IX_PamRotationJob_ClaimedByAccessConnectorId_Action' AND object_id = OBJECT_ID('[dbo].[PamRotationJob]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_PamRotationJob_ClaimedByAccessConnectorId_Action]
        ON [dbo].[PamRotationJob] ([ClaimedByAccessConnectorId] ASC, [Action] ASC);
END
GO

IF OBJECT_ID('[dbo].[PamRotationAttempt]') IS NULL
BEGIN
    CREATE TABLE [dbo].[PamRotationAttempt] (
        [Id]                            UNIQUEIDENTIFIER    NOT NULL,
        [JobId]                         UNIQUEIDENTIFIER    NOT NULL,
        [ClaimedByAccessConnectorId]    UNIQUEIDENTIFIER    NOT NULL,
        [CipherUpdated]                 BIT                 NOT NULL,
        [Action]                        TINYINT             NOT NULL,
        [FailureReason]                 NVARCHAR(500)       NULL,
        [SyncState]                     TINYINT             NULL,
        [SessionTermination]            TINYINT             NULL,
        [CreationDate]                  DATETIME2(7)        NOT NULL,
        [ResolvedDate]                  DATETIME2(7)        NULL,
        CONSTRAINT [PK_PamRotationAttempt] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_PamRotationAttempt_RotationJob] FOREIGN KEY ([JobId]) REFERENCES [dbo].[PamRotationJob] ([Id]) ON DELETE CASCADE
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'IX_PamRotationAttempt_JobId_Action' AND object_id = OBJECT_ID('[dbo].[PamRotationAttempt]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_PamRotationAttempt_JobId_Action]
        ON [dbo].[PamRotationAttempt] ([JobId] ASC, [Action] ASC);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'IX_PamRotationAttempt_ClaimedByAccessConnectorId_JobId' AND object_id = OBJECT_ID('[dbo].[PamRotationAttempt]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_PamRotationAttempt_ClaimedByAccessConnectorId_JobId]
        ON [dbo].[PamRotationAttempt] ([ClaimedByAccessConnectorId] ASC, [JobId] ASC);
END
GO

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

IF OBJECT_ID('[dbo].[PamRotationJobTimeoutSweep]') IS NULL
BEGIN
    CREATE TABLE [dbo].[PamRotationJobTimeoutSweep] (
        [RotationJobId] UNIQUEIDENTIFIER    NOT NULL,
        [SweptDate]     DATETIME2(7)        NOT NULL,
        CONSTRAINT [PK_PamRotationJobTimeoutSweep] PRIMARY KEY CLUSTERED ([RotationJobId] ASC),
        CONSTRAINT [FK_PamRotationJobTimeoutSweep_PamRotationJob] FOREIGN KEY ([RotationJobId]) REFERENCES [dbo].[PamRotationJob] ([Id]) ON DELETE CASCADE
    );
END
GO

CREATE OR ALTER PROCEDURE [dbo].[PamAccessConnectorDetails_ReadByApiKeyId]
    @ApiKeyId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    -- Token-issuance lookup: includes the org's Enabled/UsePam flags.
    -- A lapsed org can't mint a token this way.
    SELECT
        D.*,
        O.[Enabled] AS [OrganizationEnabled],
        O.[UsePam] AS [OrganizationUsePam]
    FROM [dbo].[PamAccessConnector] D
    INNER JOIN [dbo].[Organization] O ON O.[Id] = D.[OrganizationId]
    WHERE D.[ApiKeyId] = @ApiKeyId
END
GO

CREATE OR ALTER PROCEDURE [dbo].[PamAccessConnectorTargetAssignment_Create]
    @Id UNIQUEIDENTIFIER,
    @AccessConnectorId UNIQUEIDENTIFIER,
    @TargetSystemId UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @CreationDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    -- @Id is a plain input, not OUTPUT; caller assigns it first.
    -- Unique index backstops OneAssignmentPerConnectorTarget on a race.
    INSERT INTO [dbo].[PamAccessConnectorTargetAssignment]
    (
        [Id],
        [AccessConnectorId],
        [TargetSystemId],
        [OrganizationId],
        [CreationDate]
    )
    VALUES
    (
        @Id,
        @AccessConnectorId,
        @TargetSystemId,
        @OrganizationId,
        @CreationDate
    )
END
GO

CREATE OR ALTER PROCEDURE [dbo].[PamAccessConnectorTargetAssignment_DeleteByAccessConnectorIdTargetSystemId]
    @AccessConnectorId UNIQUEIDENTIFIER,
    @TargetSystemId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DELETE FROM [dbo].[PamAccessConnectorTargetAssignment]
    WHERE [AccessConnectorId] = @AccessConnectorId AND [TargetSystemId] = @TargetSystemId
END
GO

CREATE OR ALTER PROCEDURE [dbo].[PamAccessConnectorTargetAssignment_ExistsByAccessConnectorIdTargetSystemId]
    @AccessConnectorId UNIQUEIDENTIFIER,
    @TargetSystemId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT 1
    FROM [dbo].[PamAccessConnectorTargetAssignment]
    WHERE [AccessConnectorId] = @AccessConnectorId AND [TargetSystemId] = @TargetSystemId
END
GO

CREATE OR ALTER PROCEDURE [dbo].[PamAccessConnectorTargetAssignment_ReadByAccessConnectorId]
    @AccessConnectorId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT *
    FROM [dbo].[PamAccessConnectorTargetAssignment]
    WHERE [AccessConnectorId] = @AccessConnectorId
END
GO

CREATE OR ALTER PROCEDURE [dbo].[PamAccessConnectorTargetAssignment_ReadByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT *
    FROM [dbo].[PamAccessConnectorTargetAssignment]
    WHERE [OrganizationId] = @OrganizationId
END
GO

CREATE OR ALTER PROCEDURE [dbo].[PamAccessConnector_Create]
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

    INSERT INTO [dbo].[PamAccessConnector]
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

CREATE OR ALTER PROCEDURE [dbo].[PamAccessConnector_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON
    -- Cascade in one transaction: NO ACTION FKs mean assignments, then access connector, then credential.
    SET XACT_ABORT ON

    DECLARE @Now DATETIME2(7) = GETUTCDATE()
    DECLARE @ApiKeyId UNIQUEIDENTIFIER

    BEGIN TRANSACTION

    SELECT @ApiKeyId = [ApiKeyId]
    FROM [dbo].[PamAccessConnector]
    WHERE [Id] = @Id

    -- No FK ties PamRotationJob to PamAccessConnector, so this releases the access connector's live claims
    -- directly. Their attempts derive as Abandoned; only the end is recorded. A timed-out claim is left as it was.
    UPDATE AT
    SET AT.[ResolvedDate] = @Now
    FROM [dbo].[PamRotationAttempt] AT
    INNER JOIN [dbo].[PamRotationJob] J ON J.[Id] = AT.[JobId]
    WHERE AT.[Action] = 0 -- None
        AND AT.[ResolvedDate] IS NULL
        AND J.[ClaimedByAccessConnectorId] = @Id
        AND J.[Action] = 1 -- Claimed
        AND J.[ExpiresAt] > @Now

    UPDATE [dbo].[PamRotationJob]
    SET [Action] = 0, -- None
        [ClaimedByAccessConnectorId] = NULL,
        [ClaimedAt] = NULL,
        [NextClaimableAt] = @Now
    WHERE [ClaimedByAccessConnectorId] = @Id
        AND [Action] = 1 -- Claimed
        AND [ExpiresAt] > @Now

    DELETE FROM [dbo].[PamAccessConnectorTargetAssignment]
    WHERE [AccessConnectorId] = @Id

    DELETE FROM [dbo].[PamAccessConnector]
    WHERE [Id] = @Id

    DELETE FROM [dbo].[ApiKey]
    WHERE [Id] = @ApiKeyId

    COMMIT TRANSACTION
END
GO

CREATE OR ALTER PROCEDURE [dbo].[PamAccessConnector_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT *
    FROM [dbo].[PamAccessConnector]
    WHERE [Id] = @Id
END
GO

CREATE OR ALTER PROCEDURE [dbo].[PamAccessConnector_ReadByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT *
    FROM [dbo].[PamAccessConnector]
    WHERE [OrganizationId] = @OrganizationId
END
GO

CREATE OR ALTER PROCEDURE [dbo].[PamAccessConnector_Update]
    @Id UNIQUEIDENTIFIER,
    @Name NVARCHAR(200),
    @Status TINYINT,
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    -- ApiKeyId and OrganizationId never change, and LastHeartbeatAt has its own conditional bump.
    UPDATE
        [dbo].[PamAccessConnector]
    SET
        [Name] = @Name,
        [Status] = @Status,
        [RevisionDate] = @RevisionDate
    WHERE
        [Id] = @Id
END
GO

CREATE OR ALTER PROCEDURE [dbo].[PamAccessConnector_UpdateHeartbeat]
    @Id UNIQUEIDENTIFIER,
    @Now DATETIME2(7),
    @MinIntervalSeconds INT
AS
BEGIN
    SET NOCOUNT ON
    -- Conditional bump: WHERE guard no-ops most calls, updating only after @MinIntervalSeconds.
    -- Called only by the access connector's own requests, never by a sweep.
    UPDATE [dbo].[PamAccessConnector]
    SET [LastHeartbeatAt] = @Now
    WHERE [Id] = @Id
        AND ([LastHeartbeatAt] IS NULL OR [LastHeartbeatAt] < DATEADD(SECOND, -@MinIntervalSeconds, @Now))
END
GO

CREATE OR ALTER PROCEDURE [dbo].[PamRotationAttempt_AcceptCipherWrite]
    @AttemptId UNIQUEIDENTIFIER,
    @AccessConnectorId UNIQUEIDENTIFIER,
    @CipherData NVARCHAR(MAX),
    @LastKnownRevisionDate DATETIME2(7),
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON
    -- WITH (UPDLOCK) closes the check-then-act window before the cipher write.
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
        AND AT.[Action] = 0 -- None
        AND AT.[ClaimedByAccessConnectorId] = @AccessConnectorId
        AND J.[Action] = 1 -- Claimed
        AND J.[ExpiresAt] > @Now
        AND J.[ClaimedByAccessConnectorId] = @AccessConnectorId
        AND J.[ClaimedAt] = AT.[CreationDate]

    IF @VerifiedJobId IS NULL
    BEGIN
        -- Unknown attempt, wrong claimant, or an already-resolved job/attempt; caller audits as write_rejected.
        ROLLBACK TRANSACTION
        SELECT 0 -- Rejected
        RETURN
    END

    DECLARE @CipherRevisionDate DATETIME2(7) = (SELECT [RevisionDate] FROM [dbo].[Cipher] WHERE [Id] = @CipherId)

    -- The config has no FK to Cipher, so the cipher may have been hard-deleted.
    IF @CipherRevisionDate IS NULL
    BEGIN
        ROLLBACK TRANSACTION
        SELECT 0 -- Rejected
        RETURN
    END

    -- Drifted LastKnownRevisionDate means a concurrent edit; rejected, matching CipherService's tolerance.
    IF ABS(DATEDIFF_BIG(MILLISECOND, @CipherRevisionDate, @LastKnownRevisionDate)) > 1000
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

    -- Other writers of dbo.Cipher bump here too, so clients do not keep a stale password.
    EXEC [dbo].[User_BumpAccountRevisionDateByCipherId] @CipherId, @OrganizationId

    COMMIT TRANSACTION

    SELECT 1 -- Accepted
END
GO

CREATE OR ALTER PROCEDURE [dbo].[PamRotationAttempt_MarkErrored]
    @AttemptId UNIQUEIDENTIFIER,
    @AccessConnectorId UNIQUEIDENTIFIER,
    @FailureReason NVARCHAR(500) = NULL,
    @SyncState TINYINT,
    @Now DATETIME2(7),
    @MaxAttempts INT,
    @RetryBaseDelaySeconds INT
AS
BEGIN
    SET NOCOUNT ON
    -- @FailureReason is pre-bounded by the caller's zero-knowledge contract; guard failure takes RejectStaleFailureReport.
    SET XACT_ABORT ON

    BEGIN TRANSACTION

    DECLARE @JobId UNIQUEIDENTIFIER

    -- Executing is derived: unreported, on an unexpired claim, and created by the claim the job still records.
    SELECT @JobId = J.[Id]
    FROM [dbo].[PamRotationAttempt] AT
    INNER JOIN [dbo].[PamRotationJob] J WITH (UPDLOCK) ON J.[Id] = AT.[JobId]
    WHERE AT.[Id] = @AttemptId
        AND AT.[Action] = 0 -- None
        AND AT.[ClaimedByAccessConnectorId] = @AccessConnectorId
        AND J.[Action] = 1 -- Claimed
        AND J.[ExpiresAt] > @Now
        AND J.[ClaimedByAccessConnectorId] = @AccessConnectorId
        AND J.[ClaimedAt] = AT.[CreationDate]

    IF @JobId IS NULL
    BEGIN
        ROLLBACK TRANSACTION
        SELECT 0 AS [Outcome], NULL AS [JobStatus], NULL AS [ErroredAttemptCount] -- Rejected
        RETURN
    END

    UPDATE [dbo].[PamRotationAttempt]
    SET [Action] = 2, -- Errored
        [FailureReason] = @FailureReason,
        [SyncState] = @SyncState,
        [ResolvedDate] = @Now
    WHERE [Id] = @AttemptId

    -- Only Errored attempts count toward the retry budget; Abandoned (released/timed-out) tries never do.
    DECLARE @ErroredCount INT

    SELECT @ErroredCount = COUNT(*)
    FROM [dbo].[PamRotationAttempt]
    WHERE [JobId] = @JobId AND [Action] = 2 -- Errored

    -- The status the job derives as right after this write; the guard saw it unexpired.
    DECLARE @JobStatus TINYINT

    IF @ErroredCount < @MaxAttempts
    BEGIN
        SET @JobStatus = 0 -- Pending
        UPDATE [dbo].[PamRotationJob]
        SET [Action] = 0, -- None
            [ClaimedByAccessConnectorId] = NULL,
            [ClaimedAt] = NULL,
            [NextClaimableAt] = DATEADD(SECOND, CAST(@RetryBaseDelaySeconds * POWER(2, @ErroredCount - 1) AS INT), @Now)
        WHERE [Id] = @JobId
    END
    ELSE
    BEGIN
        SET @JobStatus = 3 -- Failed
        UPDATE [dbo].[PamRotationJob]
        SET [Action] = 3, -- Failed
            [ClaimedByAccessConnectorId] = NULL,
            [ClaimedAt] = NULL
        WHERE [Id] = @JobId
    END

    COMMIT TRANSACTION

    SELECT 1 AS [Outcome], @JobStatus AS [JobStatus], @ErroredCount AS [ErroredAttemptCount] -- Resolved
END
GO

CREATE OR ALTER PROCEDURE [dbo].[PamRotationAttempt_MarkRotated]
    @AttemptId UNIQUEIDENTIFIER,
    @AccessConnectorId UNIQUEIDENTIFIER,
    @SessionTermination TINYINT,
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON
    -- CipherUpdated = 1 backstops VerifiedBeforeSuccess; guard failure takes RejectStaleSuccess.
    SET XACT_ABORT ON

    BEGIN TRANSACTION

    DECLARE @JobId UNIQUEIDENTIFIER

    -- Executing is derived: unreported, on an unexpired claim, and created by the claim the job still records.
    SELECT @JobId = J.[Id]
    FROM [dbo].[PamRotationAttempt] AT
    INNER JOIN [dbo].[PamRotationJob] J WITH (UPDLOCK) ON J.[Id] = AT.[JobId]
    WHERE AT.[Id] = @AttemptId
        AND AT.[Action] = 0 -- None
        AND AT.[ClaimedByAccessConnectorId] = @AccessConnectorId
        AND AT.[CipherUpdated] = 1
        AND J.[Action] = 1 -- Claimed
        AND J.[ExpiresAt] > @Now
        AND J.[ClaimedByAccessConnectorId] = @AccessConnectorId
        AND J.[ClaimedAt] = AT.[CreationDate]

    IF @JobId IS NULL
    BEGIN
        ROLLBACK TRANSACTION
        SELECT 0 -- Rejected
        RETURN
    END

    UPDATE [dbo].[PamRotationAttempt]
    SET [Action] = 1, -- Rotated
        [SessionTermination] = @SessionTermination,
        [ResolvedDate] = @Now
    WHERE [Id] = @AttemptId

    -- Written with the attempt, so a Rotated attempt always has a Succeeded job: success wins by construction.
    UPDATE [dbo].[PamRotationJob]
    SET [Action] = 2, -- Succeeded
        [ClaimedByAccessConnectorId] = NULL,
        [ClaimedAt] = NULL
    WHERE [Id] = @JobId

    COMMIT TRANSACTION

    SELECT 1 -- Resolved
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

CREATE OR ALTER PROCEDURE [dbo].[PamRotationConfig_AnyByTargetSystem]
    @TargetSystemId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    -- DeleteTargetSystemCommand's guard; the delete itself re-checks under lock separately.
    SELECT 1
    FROM [dbo].[PamRotationConfig]
    WHERE [TargetSystemId] = @TargetSystemId
END
GO

CREATE OR ALTER PROCEDURE [dbo].[PamRotationConfig_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @OrganizationId UNIQUEIDENTIFIER,
    @CipherId UNIQUEIDENTIFIER,
    @TargetSystemId UNIQUEIDENTIFIER,
    @SealedData NVARCHAR(MAX),
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
        [SealedData],
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
        @SealedData,
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

CREATE OR ALTER PROCEDURE [dbo].[PamRotationConfig_DeleteWithJobs]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON
    -- Hard-deletes the config; its jobs, their attempts and the timeout journal go with it through the FK cascade.
    SET XACT_ABORT ON

    BEGIN TRANSACTION

    -- Re-checks under PamRotationJob_Create's range lock so a mid-window claim can't be hard-deleted, nor a timeout
    -- the sweep has yet to record.
    IF EXISTS (
        SELECT 1
        FROM [dbo].[PamRotationJob] J WITH (UPDLOCK, HOLDLOCK)
        WHERE J.[RotationConfigId] = @Id
            AND J.[Action] IN (0, 1) -- None, Claimed
            AND NOT EXISTS (
                SELECT 1
                FROM [dbo].[PamRotationJobTimeoutSweep] S
                WHERE S.[RotationJobId] = J.[Id]
            )
    )
    BEGIN
        ROLLBACK TRANSACTION
        SELECT 0 -- ActiveJobExists
        RETURN
    END

    DELETE FROM [dbo].[PamRotationConfig]
    WHERE [Id] = @Id

    COMMIT TRANSACTION

    SELECT 1 -- Deleted
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

CREATE OR ALTER PROCEDURE [dbo].[PamRotationConfig_ReadDetailsById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    -- Header projection: denormalizes target name/method, computes HasActiveJob to avoid a second round trip.
    -- A timed-out job stays active until the timeout sweep records it.
    SELECT
        C.*,
        T.[Name] AS [TargetSystemName],
        T.[Method] AS [TargetSystemMethod],
        CASE WHEN EXISTS (
            SELECT 1
            FROM [dbo].[PamRotationJob] J
            WHERE J.[RotationConfigId] = C.[Id]
                AND J.[Action] IN (0, 1) -- None, Claimed
                AND NOT EXISTS (
                    SELECT 1
                    FROM [dbo].[PamRotationJobTimeoutSweep] S
                    WHERE S.[RotationJobId] = J.[Id]
                )
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

    -- Schedule-list view: denormalizes target name/method, computes HasActiveJob to avoid an N+1/round trip.
    -- A timed-out job stays active until the timeout sweep records it.
    SELECT
        C.*,
        T.[Name] AS [TargetSystemName],
        T.[Method] AS [TargetSystemMethod],
        CASE WHEN EXISTS (
            SELECT 1
            FROM [dbo].[PamRotationJob] J
            WHERE J.[RotationConfigId] = C.[Id]
                AND J.[Action] IN (0, 1) -- None, Claimed
                AND NOT EXISTS (
                    SELECT 1
                    FROM [dbo].[PamRotationJobTimeoutSweep] S
                    WHERE S.[RotationJobId] = J.[Id]
                )
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

    -- The sweep's due phase; feeds OfferRotationCommand, one call per row.
    -- Matches [IX_PamRotationConfig_NextRotationAt] for a range seek, not a scan.
    SELECT C.*
    FROM [dbo].[PamRotationConfig] C
    INNER JOIN [dbo].[PamTargetSystem] T ON T.[Id] = C.[TargetSystemId]
    WHERE C.[Enabled] = 1
        AND C.[NextRotationAt] IS NOT NULL
        AND C.[NextRotationAt] <= @Now
        AND T.[Method] = 0 -- Automatic
        AND T.[Status] = 0 -- Active
        -- Until the timeout sweep records a timed-out job it still blocks, so its reschedule is never raced.
        AND NOT EXISTS (
            SELECT 1
            FROM [dbo].[PamRotationJob] J
            WHERE J.[RotationConfigId] = C.[Id]
                AND J.[Action] IN (0, 1) -- None, Claimed
                AND NOT EXISTS (
                    SELECT 1
                    FROM [dbo].[PamRotationJobTimeoutSweep] S
                    WHERE S.[RotationJobId] = J.[Id]
                )
        )
END
GO

CREATE OR ALTER PROCEDURE [dbo].[PamRotationConfig_Update]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @CipherId UNIQUEIDENTIFIER,
    @TargetSystemId UNIQUEIDENTIFIER,
    @SealedData NVARCHAR(MAX),
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
        [SealedData] = @SealedData,
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

CREATE OR ALTER PROCEDURE [dbo].[PamRotationJob_Claim]
    @JobId UNIQUEIDENTIFIER,
    @AttemptId UNIQUEIDENTIFIER,
    @AccessConnectorId UNIQUEIDENTIFIER,
    @Now DATETIME2(7),
    @ReleaseDelaySeconds INT
AS
BEGIN
    SET NOCOUNT ON
    -- First-claim-wins: the UPDATE's WHERE Action = 0 takes the row lock, serializing concurrent claims.
    SET XACT_ABORT ON

    BEGIN TRANSACTION

    UPDATE J
    SET J.[Action] = 1, -- Claimed
        J.[ClaimedByAccessConnectorId] = @AccessConnectorId,
        J.[ClaimedAt] = @Now
    FROM [dbo].[PamRotationJob] J
    INNER JOIN [dbo].[PamRotationConfig] C ON C.[Id] = J.[RotationConfigId]
    INNER JOIN [dbo].[PamTargetSystem] T ON T.[Id] = C.[TargetSystemId]
    INNER JOIN [dbo].[PamAccessConnectorTargetAssignment] A ON A.[AccessConnectorId] = @AccessConnectorId AND A.[TargetSystemId] = C.[TargetSystemId]
    -- Re-checks Enabled and the org match, which the caller's token already checked.
    INNER JOIN [dbo].[PamAccessConnector] D ON D.[Id] = @AccessConnectorId AND D.[OrganizationId] = C.[OrganizationId] AND D.[Status] = 0 -- Enabled
    WHERE J.[Id] = @JobId
        AND J.[Action] = 0 -- None
        AND J.[ExpiresAt] > @Now
        AND J.[NextClaimableAt] <= @Now
        AND C.[Enabled] = 1
        AND T.[Status] = 0 -- Active

    IF @@ROWCOUNT = 0
    BEGIN
        -- Unknown job or one outside this access connector's assignment share NotEligible; no existence oracle.
        DECLARE @Outcome INT = CASE
            WHEN NOT EXISTS (
                SELECT 1
                FROM [dbo].[PamRotationJob] J2
                INNER JOIN [dbo].[PamRotationConfig] C2 ON C2.[Id] = J2.[RotationConfigId]
                INNER JOIN [dbo].[PamAccessConnectorTargetAssignment] A2 ON A2.[AccessConnectorId] = @AccessConnectorId AND A2.[TargetSystemId] = C2.[TargetSystemId]
                INNER JOIN [dbo].[PamAccessConnector] D2 ON D2.[Id] = @AccessConnectorId AND D2.[OrganizationId] = C2.[OrganizationId] AND D2.[Status] = 0 -- Enabled
                WHERE J2.[Id] = @JobId
            ) THEN -1 -- NotEligible: unknown, outside the assignment or org, or the access connector is disabled
            ELSE 0 -- NotClaimable: eligible, but not Pending, in backoff, or its config or target is disabled
        END

        ROLLBACK TRANSACTION

        SELECT
            @Outcome AS [Outcome],
            CAST(NULL AS UNIQUEIDENTIFIER) AS [AttemptId],
            CAST(NULL AS UNIQUEIDENTIFIER) AS [JobId],
            CAST(NULL AS TINYINT) AS [Source],
            CAST(NULL AS UNIQUEIDENTIFIER) AS [TargetSystemId],
            CAST(NULL AS NVARCHAR(200)) AS [TargetSystemName],
            CAST(NULL AS NVARCHAR(MAX)) AS [TargetSystemSealedData],
            CAST(NULL AS UNIQUEIDENTIFIER) AS [CipherId],
            CAST(NULL AS NVARCHAR(MAX)) AS [RotationConfigSealedData],
            CAST(NULL AS DATETIME2(7)) AS [ExecuteBy]
        RETURN
    END

    -- AtMostOneInFlightAttemptPerJob: the attempt is created in the same transaction as the claim.
    -- Its CreationDate equals the job's ClaimedAt, which is how the claim's own attempt is recognised later.
    INSERT INTO [dbo].[PamRotationAttempt]
    (
        [Id], [JobId], [ClaimedByAccessConnectorId], [CipherUpdated], [Action], [FailureReason], [SyncState],
        [SessionTermination], [CreationDate], [ResolvedDate]
    )
    VALUES
    (
        @AttemptId, @JobId, @AccessConnectorId, 0, 0 /* None */, NULL, NULL,
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
        T.[SealedData] AS [TargetSystemSealedData],
        C.[CipherId],
        C.[SealedData] AS [RotationConfigSealedData],
        DATEADD(SECOND, @ReleaseDelaySeconds, @Now) AS [ExecuteBy]
    FROM [dbo].[PamRotationJob] J
    INNER JOIN [dbo].[PamRotationConfig] C ON C.[Id] = J.[RotationConfigId]
    INNER JOIN [dbo].[PamTargetSystem] T ON T.[Id] = C.[TargetSystemId]
    WHERE J.[Id] = @JobId
END
GO

CREATE OR ALTER PROCEDURE [dbo].[PamRotationJob_Create]
    @Id UNIQUEIDENTIFIER,
    @RotationConfigId UNIQUEIDENTIFIER,
    @Source TINYINT,
    @Action TINYINT,
    @ClaimedByAccessConnectorId UNIQUEIDENTIFIER = NULL,
    @ClaimedAt DATETIME2(7) = NULL,
    @CreationDate DATETIME2(7),
    @NextClaimableAt DATETIME2(7),
    @ExpiresAt DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON
    -- Caller passes an already-populated unclaimed job; this only re-validates eligibility and the guard.
    -- Holds the range lock until the INSERT commits.
    SET XACT_ABORT ON

    BEGIN TRANSACTION

    -- Re-checked so a config/target disabled between read and write can't mint a job.
    -- Outcome -1 (ConfigNotOfferable) is distinct from 0 (ActiveJobExists).
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

    -- AtMostOneActiveJobPerConfig: range lock holds for the transaction, blocking concurrent creation.
    -- A timed-out job holds its config until the timeout sweep records it, so the sweep's reschedule lands first.
    IF EXISTS (
        SELECT 1
        FROM [dbo].[PamRotationJob] J WITH (UPDLOCK, HOLDLOCK)
        WHERE J.[RotationConfigId] = @RotationConfigId
            AND J.[Action] IN (0, 1) -- None, Claimed
            AND NOT EXISTS (
                SELECT 1
                FROM [dbo].[PamRotationJobTimeoutSweep] S
                WHERE S.[RotationJobId] = J.[Id]
            )
    )
    BEGIN
        ROLLBACK TRANSACTION
        SELECT 0 -- ActiveJobExists
        RETURN
    END

    INSERT INTO [dbo].[PamRotationJob]
    (
        [Id], [RotationConfigId], [Source], [Action], [ClaimedByAccessConnectorId], [ClaimedAt],
        [CreationDate], [NextClaimableAt], [ExpiresAt]
    )
    VALUES
    (
        @Id, @RotationConfigId, @Source, @Action, @ClaimedByAccessConnectorId, @ClaimedAt,
        @CreationDate, @NextClaimableAt, @ExpiresAt
    )

    COMMIT TRANSACTION

    SELECT 1 -- Created
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

    -- Two result sets let the caller zip jobs to attempts, grouped by JobId.
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

CREATE OR ALTER PROCEDURE [dbo].[PamRotationJob_ReadManyClaimableByAccessConnectorId]
    @AccessConnectorId UNIQUEIDENTIFIER,
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    -- The access connector poll; re-derives PamRotationJob_Claim's eligibility so the poll list matches what's claimable.
    SELECT J.*, C.[TargetSystemId]
    FROM [dbo].[PamRotationJob] J
    INNER JOIN [dbo].[PamRotationConfig] C ON C.[Id] = J.[RotationConfigId]
    INNER JOIN [dbo].[PamTargetSystem] T ON T.[Id] = C.[TargetSystemId]
    INNER JOIN [dbo].[PamAccessConnectorTargetAssignment] A ON A.[AccessConnectorId] = @AccessConnectorId AND A.[TargetSystemId] = C.[TargetSystemId]
    INNER JOIN [dbo].[PamAccessConnector] D ON D.[Id] = @AccessConnectorId AND D.[OrganizationId] = C.[OrganizationId] AND D.[Status] = 0 -- Enabled
    WHERE J.[Action] = 0 -- None
        AND J.[ExpiresAt] > @Now
        AND J.[NextClaimableAt] <= @Now
        AND C.[Enabled] = 1
        AND T.[Status] = 0 -- Active
END
GO

CREATE OR ALTER PROCEDURE [dbo].[PamRotationJob_ReadManyRecentByAccessConnectorId]
    @AccessConnectorId UNIQUEIDENTIFIER,
    @Limit INT
AS
BEGIN
    SET NOCOUNT ON

    -- Two result sets (jobs, attempts); membership is by the attempt's ClaimedByAccessConnectorId.
    SELECT TOP (@Limit) J.*
    INTO #Jobs
    FROM [dbo].[PamRotationJob] J
    WHERE EXISTS (
        SELECT 1
        FROM [dbo].[PamRotationAttempt] A
        WHERE A.[JobId] = J.[Id]
            AND A.[ClaimedByAccessConnectorId] = @AccessConnectorId
    )
    ORDER BY J.[CreationDate] DESC

    SELECT *
    FROM #Jobs
    ORDER BY [CreationDate] DESC

    SELECT A.*
    FROM [dbo].[PamRotationAttempt] A
    INNER JOIN #Jobs J ON J.[Id] = A.[JobId]
    WHERE A.[ClaimedByAccessConnectorId] = @AccessConnectorId
    ORDER BY A.[JobId], A.[CreationDate] ASC

    DROP TABLE #Jobs
END
GO

CREATE OR ALTER PROCEDURE [dbo].[PamRotationJob_ReleaseExpiredLeases]
    @Now DATETIME2(7),
    @OfflineAfterSeconds INT,
    @ReleaseDelaySeconds INT
AS
BEGIN
    SET NOCOUNT ON
    -- Releases require an expired lease and a stale heartbeat, never Action alone. A Rotated attempt always comes
    -- with a Succeeded job, so success wins without checking attempts.
    SET XACT_ABORT ON

    BEGIN TRANSACTION

    DECLARE @Affected TABLE (
        [JobId] UNIQUEIDENTIFIER NOT NULL,
        [PreviousClaimedByAccessConnectorId] UNIQUEIDENTIFIER NULL
    )

    UPDATE J
    SET J.[Action] = 0, -- None
        -- Uses the pre-clear ClaimedAt, still visible here, so re-claim time equals ExecuteBy.
        J.[NextClaimableAt] = DATEADD(SECOND, @ReleaseDelaySeconds, J.[ClaimedAt]),
        J.[ClaimedByAccessConnectorId] = NULL,
        J.[ClaimedAt] = NULL
    OUTPUT deleted.[Id], deleted.[ClaimedByAccessConnectorId] INTO @Affected ([JobId], [PreviousClaimedByAccessConnectorId])
    FROM [dbo].[PamRotationJob] J
    INNER JOIN [dbo].[PamAccessConnector] D ON D.[Id] = J.[ClaimedByAccessConnectorId]
    WHERE J.[Action] = 1 -- Claimed
        AND J.[ExpiresAt] > @Now -- a timed-out claim is the timeout sweep's, not a release
        AND DATEADD(SECOND, @ReleaseDelaySeconds, J.[ClaimedAt]) <= @Now
        AND (D.[LastHeartbeatAt] IS NULL OR D.[LastHeartbeatAt] < DATEADD(SECOND, -@OfflineAfterSeconds, @Now))

    -- Records when the released claim's attempt ended; it derives as Abandoned, which the retry budget never charges.
    UPDATE [dbo].[PamRotationAttempt]
    SET [ResolvedDate] = @Now
    WHERE [JobId] IN (SELECT [JobId] FROM @Affected)
        AND [Action] = 0 -- None
        AND [ResolvedDate] IS NULL

    -- One row per released job; ClaimedByAccessConnectorId is the pre-clear claimant, always non-null.
    SELECT
        AF.[JobId],
        C.[Id] AS [RotationConfigId],
        C.[OrganizationId],
        C.[CipherId],
        J.[Source],
        AF.[PreviousClaimedByAccessConnectorId] AS [ClaimedByAccessConnectorId]
    FROM @Affected AF
    INNER JOIN [dbo].[PamRotationJob] J ON J.[Id] = AF.[JobId]
    INNER JOIN [dbo].[PamRotationConfig] C ON C.[Id] = J.[RotationConfigId]

    COMMIT TRANSACTION
END
GO

CREATE OR ALTER PROCEDURE [dbo].[PamRotationJob_TimeoutDue]
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    -- A timeout is derived; the PamRotationJobTimeoutSweep INSERT decides which run owns a job, and UPDLOCK/HOLDLOCK
    -- makes a losing sweep skip it. A Rotated attempt always has a Succeeded job, so success wins without checking
    -- attempts.
    DECLARE @Due TABLE ([RotationJobId] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY);

    INSERT INTO [dbo].[PamRotationJobTimeoutSweep] ([RotationJobId], [SweptDate])
    OUTPUT inserted.[RotationJobId] INTO @Due
    SELECT
        J.[Id],
        @Now
    FROM [dbo].[PamRotationJob] J
    WHERE J.[Action] IN (0, 1) -- None, Claimed: unresolved, so the passed deadline is a timeout
        AND J.[ExpiresAt] <= @Now
        AND NOT EXISTS (
            SELECT 1
            FROM [dbo].[PamRotationJobTimeoutSweep] S WITH (UPDLOCK, HOLDLOCK)
            WHERE S.[RotationJobId] = J.[Id]
        )

    -- One row per timed-out job; AttemptCount tells unroutable (never claimed) from stuck (claimed).
    SELECT
        J.[Id] AS [JobId],
        C.[Id] AS [RotationConfigId],
        C.[OrganizationId],
        C.[CipherId],
        J.[Source],
        J.[ClaimedByAccessConnectorId],
        (SELECT COUNT(*) FROM [dbo].[PamRotationAttempt] AT WHERE AT.[JobId] = J.[Id]) AS [AttemptCount]
    FROM @Due D
    INNER JOIN [dbo].[PamRotationJob] J ON J.[Id] = D.[RotationJobId]
    INNER JOIN [dbo].[PamRotationConfig] C ON C.[Id] = J.[RotationConfigId]
END
GO

CREATE OR ALTER PROCEDURE [dbo].[PamTargetSystem_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @OrganizationId UNIQUEIDENTIFIER,
    @Name NVARCHAR(200),
    @Method TINYINT,
    @SealedData NVARCHAR(MAX) = NULL,
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
        [SealedData],
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
        @SealedData,
        @Status,
        @CreationDate,
        @RevisionDate
    )
END
GO

CREATE OR ALTER PROCEDURE [dbo].[PamTargetSystem_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    -- No cascade; a referenced target is blocked by its NO ACTION FKs.
    DELETE FROM [dbo].[PamTargetSystem]
    WHERE [Id] = @Id
END
GO

CREATE OR ALTER PROCEDURE [dbo].[PamTargetSystem_DeleteWithAssignments]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON
    -- Cascade deletes assignments with the target; a rotation config instead blocks the delete.
    SET XACT_ABORT ON

    BEGIN TRANSACTION

    -- Re-checks under a range lock, since the caller's guard read was outside this transaction.
    IF EXISTS (
        SELECT 1
        FROM [dbo].[PamRotationConfig] WITH (UPDLOCK, HOLDLOCK)
        WHERE [TargetSystemId] = @Id
    )
    BEGIN
        ROLLBACK TRANSACTION
        SELECT 0 -- RotationConfigExists
        RETURN
    END

    DELETE FROM [dbo].[PamAccessConnectorTargetAssignment]
    WHERE [TargetSystemId] = @Id

    DELETE FROM [dbo].[PamTargetSystem]
    WHERE [Id] = @Id

    COMMIT TRANSACTION

    SELECT 1 -- Deleted
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

CREATE OR ALTER PROCEDURE [dbo].[PamTargetSystem_Update]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @Name NVARCHAR(200),
    @Method TINYINT,
    @SealedData NVARCHAR(MAX),
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
        [SealedData] = @SealedData,
        [Status] = @Status,
        [CreationDate] = @CreationDate,
        [RevisionDate] = @RevisionDate
    WHERE
        [Id] = @Id
END
GO

CREATE OR ALTER PROCEDURE [dbo].[AccessLease_ExpireDue]
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    -- Expiry is derived, not stored; PamLeaseExpirySweep's INSERT decides which run owns a lease.
    -- UPDLOCK/HOLDLOCK serializes concurrent sweeps; a loser re-checks after commit and skips it.
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

    -- No join needed; the caller audits/triggers off the lease row itself.
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
