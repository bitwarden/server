CREATE PROCEDURE [dbo].[PamRotationJob_Claim]
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
