-- Derives rotation job and attempt status from stored facts instead of storing a status the clock can invalidate.
-- [Status] -> [Action] on both tables with their indexes renamed to match, a journal so the timeout sweep fires once
-- per job, and every procedure naming the old column re-issued. TimedOut and Abandoned have no Action; their rows
-- move to None, which re-derives the same status.

-- Column renames (preserve position, NULL-ness, and data)
IF COL_LENGTH('[dbo].[PamRotationJob]', 'Status') IS NOT NULL
    AND COL_LENGTH('[dbo].[PamRotationJob]', 'Action') IS NULL
BEGIN
    EXEC sp_rename '[dbo].[PamRotationJob].[Status]', 'Action', 'COLUMN';
END
GO

IF COL_LENGTH('[dbo].[PamRotationAttempt]', 'Status') IS NOT NULL
    AND COL_LENGTH('[dbo].[PamRotationAttempt]', 'Action') IS NULL
BEGIN
    EXEC sp_rename '[dbo].[PamRotationAttempt].[Status]', 'Action', 'COLUMN';
END
GO

-- Index renames (rename only, shapes unchanged)
IF EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'IX_PamRotationJob_RotationConfigId_Status' AND [object_id] = OBJECT_ID('[dbo].[PamRotationJob]'))
BEGIN
    EXEC sp_rename '[dbo].[PamRotationJob].[IX_PamRotationJob_RotationConfigId_Status]', 'IX_PamRotationJob_RotationConfigId_Action', 'INDEX';
END
GO
IF EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'IX_PamRotationJob_Status_ExpiresAt' AND [object_id] = OBJECT_ID('[dbo].[PamRotationJob]'))
BEGIN
    EXEC sp_rename '[dbo].[PamRotationJob].[IX_PamRotationJob_Status_ExpiresAt]', 'IX_PamRotationJob_Action_ExpiresAt', 'INDEX';
END
GO
IF EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'IX_PamRotationJob_ClaimedByAccessConnectorId_Status' AND [object_id] = OBJECT_ID('[dbo].[PamRotationJob]'))
BEGIN
    EXEC sp_rename '[dbo].[PamRotationJob].[IX_PamRotationJob_ClaimedByAccessConnectorId_Status]', 'IX_PamRotationJob_ClaimedByAccessConnectorId_Action', 'INDEX';
END
GO
IF EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'IX_PamRotationAttempt_JobId_Status' AND [object_id] = OBJECT_ID('[dbo].[PamRotationAttempt]'))
BEGIN
    EXEC sp_rename '[dbo].[PamRotationAttempt].[IX_PamRotationAttempt_JobId_Status]', 'IX_PamRotationAttempt_JobId_Action', 'INDEX';
END
GO

-- Timeout sweep journal
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

-- The old sweep already reported these timeouts; journaled so the new one doesn't report them again.
INSERT INTO [dbo].[PamRotationJobTimeoutSweep] ([RotationJobId], [SweptDate])
SELECT J.[Id], J.[ExpiresAt]
FROM [dbo].[PamRotationJob] J
WHERE J.[Action] = 4 -- TimedOut, no longer storable
    AND NOT EXISTS (
        SELECT 1
        FROM [dbo].[PamRotationJobTimeoutSweep] S
        WHERE S.[RotationJobId] = J.[Id]
    )
GO

-- Lossless: ExpiresAt is immutable and already past, so these jobs re-derive as TimedOut, and these attempts no longer
-- match their job's claim, so they re-derive as Abandoned.
UPDATE [dbo].[PamRotationJob]
SET [Action] = 0 -- None
WHERE [Action] = 4 -- TimedOut
GO

UPDATE [dbo].[PamRotationAttempt]
SET [Action] = 0 -- None
WHERE [Action] = 3 -- Abandoned
GO

-- Claim and attempt writes

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
    -- Defense in depth: re-checks Enabled and org match already checked by the caller's token.
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

-- Sweeps and releases

CREATE OR ALTER PROCEDURE [dbo].[PamRotationJob_TimeoutDue]
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    -- A timeout is derived, not stored; PamRotationJobTimeoutSweep's INSERT decides which run owns a job.
    -- UPDLOCK/HOLDLOCK serializes concurrent sweeps; a loser re-checks after commit and skips it.
    -- A Rotated attempt always comes with a Succeeded job, so success wins without checking attempts.
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
        -- Uses the pre-clear ClaimedAt, still visible here, so re-claim time is exactly ExecuteBy.
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

-- Config reads and delete

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

CREATE OR ALTER PROCEDURE [dbo].[PamRotationConfig_DeleteWithJobs]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON
    -- Cascade: hard-deletes attempts, then jobs, then config, since both FKs are NO ACTION. The timeout journal
    -- cascades from the jobs on its own.
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
