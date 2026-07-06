CREATE PROCEDURE [dbo].[PamRotationJob_Claim]
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
