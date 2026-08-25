-- PAM rotation: daemon detail read. Adds [dbo].[PamRotationJob_ReadManyRecentByDaemonId], the daemon detail page's
-- recent-activity history (GET organizations/{orgId}/rotation/daemons/{id}), plus the index it seeks on.

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

    -- Two result sets so the caller can zip each job to its attempts by JobId without an N+1:
    --   1) the @Limit most recent jobs the daemon has attempted, newest first.
    --   2) that daemon's attempts against those jobs, oldest-first within a job.
    -- Membership is decided by PamRotationAttempt.ClaimedByDaemonId, not PamRotationJob.ClaimedByDaemonId: the job's
    -- claim fields are cleared when it resolves, releases or times out, so only the attempt still records who worked
    -- it. The attempt result set is filtered to @DaemonId too, so a job two daemons worked returns only this one's.
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
