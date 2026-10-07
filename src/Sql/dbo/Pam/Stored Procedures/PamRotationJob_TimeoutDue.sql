CREATE PROCEDURE [dbo].[PamRotationJob_TimeoutDue]
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
