-- Renames the rotation daemon's tables, columns, keys and indexes to access connector in place, so existing rows
-- survive, then moves its procedures onto the new names and drops the old ones.

IF OBJECT_ID('[dbo].[PamDaemon]') IS NOT NULL AND OBJECT_ID('[dbo].[PamAccessConnector]') IS NULL
BEGIN
    EXEC sp_rename '[dbo].[PamDaemon]', 'PamAccessConnector';
END
GO

IF OBJECT_ID('[dbo].[PamDaemonTargetAssignment]') IS NOT NULL AND OBJECT_ID('[dbo].[PamAccessConnectorTargetAssignment]') IS NULL
BEGIN
    EXEC sp_rename '[dbo].[PamDaemonTargetAssignment]', 'PamAccessConnectorTargetAssignment';
END
GO

IF COL_LENGTH('[dbo].[PamAccessConnectorTargetAssignment]', 'DaemonId') IS NOT NULL
BEGIN
    EXEC sp_rename '[dbo].[PamAccessConnectorTargetAssignment].[DaemonId]', 'AccessConnectorId', 'COLUMN';
END
GO

IF COL_LENGTH('[dbo].[PamRotationJob]', 'ClaimedByDaemonId') IS NOT NULL
BEGIN
    EXEC sp_rename '[dbo].[PamRotationJob].[ClaimedByDaemonId]', 'ClaimedByAccessConnectorId', 'COLUMN';
END
GO

IF COL_LENGTH('[dbo].[PamRotationAttempt]', 'ClaimedByDaemonId') IS NOT NULL
BEGIN
    EXEC sp_rename '[dbo].[PamRotationAttempt].[ClaimedByDaemonId]', 'ClaimedByAccessConnectorId', 'COLUMN';
END
GO

IF OBJECT_ID('[dbo].[PK_PamDaemon]') IS NOT NULL
BEGIN
    EXEC sp_rename '[dbo].[PK_PamDaemon]', 'PK_PamAccessConnector', 'OBJECT';
END
GO

IF OBJECT_ID('[dbo].[FK_PamDaemon_Organization]') IS NOT NULL
BEGIN
    EXEC sp_rename '[dbo].[FK_PamDaemon_Organization]', 'FK_PamAccessConnector_Organization', 'OBJECT';
END
GO

IF OBJECT_ID('[dbo].[FK_PamDaemon_ApiKey]') IS NOT NULL
BEGIN
    EXEC sp_rename '[dbo].[FK_PamDaemon_ApiKey]', 'FK_PamAccessConnector_ApiKey', 'OBJECT';
END
GO

IF OBJECT_ID('[dbo].[PK_PamDaemonTargetAssignment]') IS NOT NULL
BEGIN
    EXEC sp_rename '[dbo].[PK_PamDaemonTargetAssignment]', 'PK_PamAccessConnectorTargetAssignment', 'OBJECT';
END
GO

IF OBJECT_ID('[dbo].[FK_PamDaemonTargetAssignment_Daemon]') IS NOT NULL
BEGIN
    EXEC sp_rename '[dbo].[FK_PamDaemonTargetAssignment_Daemon]', 'FK_PamAccessConnectorTargetAssignment_AccessConnector', 'OBJECT';
END
GO

IF OBJECT_ID('[dbo].[FK_PamDaemonTargetAssignment_TargetSystem]') IS NOT NULL
BEGIN
    EXEC sp_rename '[dbo].[FK_PamDaemonTargetAssignment_TargetSystem]', 'FK_PamAccessConnectorTargetAssignment_TargetSystem', 'OBJECT';
END
GO

IF OBJECT_ID('[dbo].[FK_PamDaemonTargetAssignment_Organization]') IS NOT NULL
BEGIN
    EXEC sp_rename '[dbo].[FK_PamDaemonTargetAssignment_Organization]', 'FK_PamAccessConnectorTargetAssignment_Organization', 'OBJECT';
END
GO

IF EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'IX_PamDaemon_ApiKeyId' AND [object_id] = OBJECT_ID('[dbo].[PamAccessConnector]'))
BEGIN
    EXEC sp_rename '[dbo].[PamAccessConnector].[IX_PamDaemon_ApiKeyId]', 'IX_PamAccessConnector_ApiKeyId', 'INDEX';
END
GO

IF EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'IX_PamDaemon_OrganizationId' AND [object_id] = OBJECT_ID('[dbo].[PamAccessConnector]'))
BEGIN
    EXEC sp_rename '[dbo].[PamAccessConnector].[IX_PamDaemon_OrganizationId]', 'IX_PamAccessConnector_OrganizationId', 'INDEX';
END
GO

IF EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'IX_PamDaemonTargetAssignment_DaemonId_TargetSystemId' AND [object_id] = OBJECT_ID('[dbo].[PamAccessConnectorTargetAssignment]'))
BEGIN
    EXEC sp_rename '[dbo].[PamAccessConnectorTargetAssignment].[IX_PamDaemonTargetAssignment_DaemonId_TargetSystemId]', 'IX_PamAccessConnectorTargetAssignment_AccessConnectorId_TargetSystemId', 'INDEX';
END
GO

IF EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'IX_PamDaemonTargetAssignment_TargetSystemId' AND [object_id] = OBJECT_ID('[dbo].[PamAccessConnectorTargetAssignment]'))
BEGIN
    EXEC sp_rename '[dbo].[PamAccessConnectorTargetAssignment].[IX_PamDaemonTargetAssignment_TargetSystemId]', 'IX_PamAccessConnectorTargetAssignment_TargetSystemId', 'INDEX';
END
GO

IF EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'IX_PamDaemonTargetAssignment_OrganizationId' AND [object_id] = OBJECT_ID('[dbo].[PamAccessConnectorTargetAssignment]'))
BEGIN
    EXEC sp_rename '[dbo].[PamAccessConnectorTargetAssignment].[IX_PamDaemonTargetAssignment_OrganizationId]', 'IX_PamAccessConnectorTargetAssignment_OrganizationId', 'INDEX';
END
GO

IF EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'IX_PamRotationJob_ClaimedByDaemonId_Status' AND [object_id] = OBJECT_ID('[dbo].[PamRotationJob]'))
BEGIN
    EXEC sp_rename '[dbo].[PamRotationJob].[IX_PamRotationJob_ClaimedByDaemonId_Status]', 'IX_PamRotationJob_ClaimedByAccessConnectorId_Status', 'INDEX';
END
GO

IF EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'IX_PamRotationAttempt_ClaimedByDaemonId_JobId' AND [object_id] = OBJECT_ID('[dbo].[PamRotationAttempt]'))
BEGIN
    EXEC sp_rename '[dbo].[PamRotationAttempt].[IX_PamRotationAttempt_ClaimedByDaemonId_JobId]', 'IX_PamRotationAttempt_ClaimedByAccessConnectorId_JobId', 'INDEX';
END
GO

IF OBJECT_ID('[dbo].[PamDaemonDetails_ReadByApiKeyId]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[PamDaemonDetails_ReadByApiKeyId];
END
GO

IF OBJECT_ID('[dbo].[PamDaemonTargetAssignment_Create]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[PamDaemonTargetAssignment_Create];
END
GO

IF OBJECT_ID('[dbo].[PamDaemonTargetAssignment_DeleteByDaemonIdTargetSystemId]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[PamDaemonTargetAssignment_DeleteByDaemonIdTargetSystemId];
END
GO

IF OBJECT_ID('[dbo].[PamDaemonTargetAssignment_ExistsByDaemonIdTargetSystemId]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[PamDaemonTargetAssignment_ExistsByDaemonIdTargetSystemId];
END
GO

IF OBJECT_ID('[dbo].[PamDaemonTargetAssignment_ReadByDaemonId]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[PamDaemonTargetAssignment_ReadByDaemonId];
END
GO

IF OBJECT_ID('[dbo].[PamDaemonTargetAssignment_ReadByOrganizationId]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[PamDaemonTargetAssignment_ReadByOrganizationId];
END
GO

IF OBJECT_ID('[dbo].[PamDaemon_Create]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[PamDaemon_Create];
END
GO

IF OBJECT_ID('[dbo].[PamDaemon_DeleteById]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[PamDaemon_DeleteById];
END
GO

IF OBJECT_ID('[dbo].[PamDaemon_ReadById]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[PamDaemon_ReadById];
END
GO

IF OBJECT_ID('[dbo].[PamDaemon_ReadByOrganizationId]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[PamDaemon_ReadByOrganizationId];
END
GO

IF OBJECT_ID('[dbo].[PamDaemon_Update]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[PamDaemon_Update];
END
GO

IF OBJECT_ID('[dbo].[PamDaemon_UpdateHeartbeat]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[PamDaemon_UpdateHeartbeat];
END
GO

IF OBJECT_ID('[dbo].[PamRotationJob_ReadManyClaimableByDaemonId]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[PamRotationJob_ReadManyClaimableByDaemonId];
END
GO

IF OBJECT_ID('[dbo].[PamRotationJob_ReadManyRecentByDaemonId]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[PamRotationJob_ReadManyRecentByDaemonId];
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

    -- The stored row decides which credential goes; the caller's ApiKeyId is not trusted.
    SELECT @ApiKeyId = [ApiKeyId]
    FROM [dbo].[PamAccessConnector]
    WHERE [Id] = @Id

    -- No FK ties PamRotationJob to PamAccessConnector, so this clears the access connector's claimed jobs directly.
    UPDATE AT
    SET AT.[Status] = 3, -- Abandoned
        AT.[ResolvedDate] = @Now
    FROM [dbo].[PamRotationAttempt] AT
    INNER JOIN [dbo].[PamRotationJob] J ON J.[Id] = AT.[JobId]
    WHERE AT.[Status] = 0 -- Executing
        AND J.[ClaimedByAccessConnectorId] = @Id
        AND J.[Status] = 1 -- Claimed

    UPDATE [dbo].[PamRotationJob]
    SET [Status] = 0, -- Pending
        [ClaimedByAccessConnectorId] = NULL,
        [ClaimedAt] = NULL,
        [NextClaimableAt] = @Now
    WHERE [ClaimedByAccessConnectorId] = @Id
        AND [Status] = 1 -- Claimed

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

    -- Name + Status only; callers use this narrow parameter set (no ApiKeyId/LastHeartbeatAt).
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
        AND AT.[Status] = 0 -- Executing
        AND AT.[ClaimedByAccessConnectorId] = @AccessConnectorId
        AND J.[Status] = 1 -- Claimed
        AND J.[ClaimedByAccessConnectorId] = @AccessConnectorId

    IF @VerifiedJobId IS NULL
    BEGIN
        -- Unknown attempt, wrong claimant, or an already-resolved job/attempt; caller audits as write_rejected.
        ROLLBACK TRANSACTION
        SELECT 0 -- Rejected
        RETURN
    END

    -- Drifted LastKnownRevisionDate means a concurrent edit; rejected, matching CipherService's tolerance.
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

    -- Other writers of dbo.Cipher bump here too, avoiding a stale password.
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

    SELECT @JobId = J.[Id]
    FROM [dbo].[PamRotationAttempt] AT
    INNER JOIN [dbo].[PamRotationJob] J WITH (UPDLOCK) ON J.[Id] = AT.[JobId]
    WHERE AT.[Id] = @AttemptId
        AND AT.[Status] = 0 -- Executing
        AND AT.[ClaimedByAccessConnectorId] = @AccessConnectorId
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

    -- Only Errored attempts count toward the retry budget; Abandoned (released/timed-out) tries never do.
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
            [ClaimedByAccessConnectorId] = NULL,
            [ClaimedAt] = NULL,
            [NextClaimableAt] = DATEADD(SECOND, CAST(@RetryBaseDelaySeconds * POWER(2, @ErroredCount - 1) AS INT), @Now)
        WHERE [Id] = @JobId
    END
    ELSE
    BEGIN
        SET @JobStatus = 3 -- Failed
        UPDATE [dbo].[PamRotationJob]
        SET [Status] = @JobStatus,
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

    SELECT @JobId = J.[Id]
    FROM [dbo].[PamRotationAttempt] AT
    INNER JOIN [dbo].[PamRotationJob] J WITH (UPDLOCK) ON J.[Id] = AT.[JobId]
    WHERE AT.[Id] = @AttemptId
        AND AT.[Status] = 0 -- Executing
        AND AT.[ClaimedByAccessConnectorId] = @AccessConnectorId
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
        [ClaimedByAccessConnectorId] = NULL,
        [ClaimedAt] = NULL
    WHERE [Id] = @JobId

    COMMIT TRANSACTION

    SELECT 1 -- Resolved
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
    -- First-claim-wins: the UPDATE's WHERE Status = 0 takes the row lock, serializing concurrent claims.
    SET XACT_ABORT ON

    BEGIN TRANSACTION

    UPDATE J
    SET J.[Status] = 1, -- Claimed
        J.[ClaimedByAccessConnectorId] = @AccessConnectorId,
        J.[ClaimedAt] = @Now
    FROM [dbo].[PamRotationJob] J
    INNER JOIN [dbo].[PamRotationConfig] C ON C.[Id] = J.[RotationConfigId]
    INNER JOIN [dbo].[PamTargetSystem] T ON T.[Id] = C.[TargetSystemId]
    INNER JOIN [dbo].[PamAccessConnectorTargetAssignment] A ON A.[AccessConnectorId] = @AccessConnectorId AND A.[TargetSystemId] = C.[TargetSystemId]
    -- Defense in depth: re-checks Enabled and org match already checked by the caller's token.
    INNER JOIN [dbo].[PamAccessConnector] D ON D.[Id] = @AccessConnectorId AND D.[OrganizationId] = C.[OrganizationId] AND D.[Status] = 0 -- Enabled
    WHERE J.[Id] = @JobId
        AND J.[Status] = 0 -- Pending
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
            ) THEN -1 -- NotEligible (unknown job, or a job outside this access connector's assignment/org)
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
        [Id], [JobId], [ClaimedByAccessConnectorId], [CipherUpdated], [Status], [FailureReason], [SyncState],
        [SessionTermination], [CreationDate], [ResolvedDate]
    )
    VALUES
    (
        @AttemptId, @JobId, @AccessConnectorId, 0, 0 /* Executing */, NULL, NULL,
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

CREATE OR ALTER PROCEDURE [dbo].[PamRotationJob_Create]
    @Id UNIQUEIDENTIFIER,
    @RotationConfigId UNIQUEIDENTIFIER,
    @Source TINYINT,
    @Status TINYINT,
    @ClaimedByAccessConnectorId UNIQUEIDENTIFIER = NULL,
    @ClaimedAt DATETIME2(7) = NULL,
    @CreationDate DATETIME2(7),
    @NextClaimableAt DATETIME2(7),
    @ExpiresAt DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON
    -- Caller passes an already-populated Pending job; this only re-validates eligibility and the guard.
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
        [Id], [RotationConfigId], [Source], [Status], [ClaimedByAccessConnectorId], [ClaimedAt],
        [CreationDate], [NextClaimableAt], [ExpiresAt]
    )
    VALUES
    (
        @Id, @RotationConfigId, @Source, @Status, @ClaimedByAccessConnectorId, @ClaimedAt,
        @CreationDate, @NextClaimableAt, @ExpiresAt
    )

    COMMIT TRANSACTION

    SELECT 1 -- Created
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
    WHERE J.[Status] = 0 -- Pending
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
    -- Releases require an expired lease and a stale heartbeat, never Status alone; excludes Rotated.
    SET XACT_ABORT ON

    BEGIN TRANSACTION

    DECLARE @Affected TABLE (
        [JobId] UNIQUEIDENTIFIER NOT NULL,
        [PreviousClaimedByAccessConnectorId] UNIQUEIDENTIFIER NULL
    )

    UPDATE J
    SET J.[Status] = 0, -- Pending
        -- Uses the pre-clear ClaimedAt, still visible here, so re-claim time is exactly ExecuteBy.
        J.[NextClaimableAt] = DATEADD(SECOND, @ReleaseDelaySeconds, J.[ClaimedAt]),
        J.[ClaimedByAccessConnectorId] = NULL,
        J.[ClaimedAt] = NULL
    OUTPUT deleted.[Id], deleted.[ClaimedByAccessConnectorId] INTO @Affected ([JobId], [PreviousClaimedByAccessConnectorId])
    FROM [dbo].[PamRotationJob] J
    INNER JOIN [dbo].[PamAccessConnector] D ON D.[Id] = J.[ClaimedByAccessConnectorId]
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
    -- Success wins: excludes a Rotated job past ExpiresAt; both updates commit together.
    SET XACT_ABORT ON

    BEGIN TRANSACTION

    DECLARE @Affected TABLE (
        [JobId] UNIQUEIDENTIFIER NOT NULL,
        [PreviousClaimedByAccessConnectorId] UNIQUEIDENTIFIER NULL
    )

    UPDATE J
    SET J.[Status] = 4, -- TimedOut
        J.[ClaimedByAccessConnectorId] = NULL,
        J.[ClaimedAt] = NULL
    OUTPUT deleted.[Id], deleted.[ClaimedByAccessConnectorId] INTO @Affected ([JobId], [PreviousClaimedByAccessConnectorId])
    FROM [dbo].[PamRotationJob] J
    WHERE J.[Status] IN (0, 1) -- Pending, Claimed
        AND J.[ExpiresAt] <= @Now
        AND NOT EXISTS (
            SELECT 1
            FROM [dbo].[PamRotationAttempt] AT
            WHERE AT.[JobId] = J.[Id] AND AT.[Status] = 1 -- Rotated
        )

    -- Abandons the executing attempt on each timed-out job; doesn't count against the retry budget.
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
        AF.[PreviousClaimedByAccessConnectorId] AS [ClaimedByAccessConnectorId],
        (SELECT COUNT(*) FROM [dbo].[PamRotationAttempt] AT WHERE AT.[JobId] = AF.[JobId]) AS [AttemptCount]
    FROM @Affected AF
    INNER JOIN [dbo].[PamRotationJob] J ON J.[Id] = AF.[JobId]
    INNER JOIN [dbo].[PamRotationConfig] C ON C.[Id] = J.[RotationConfigId]

    COMMIT TRANSACTION
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
