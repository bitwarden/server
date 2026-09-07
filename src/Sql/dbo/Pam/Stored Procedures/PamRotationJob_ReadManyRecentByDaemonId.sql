CREATE PROCEDURE [dbo].[PamRotationJob_ReadManyRecentByDaemonId]
    @DaemonId UNIQUEIDENTIFIER,
    @Limit INT
AS
BEGIN
    SET NOCOUNT ON

    -- Two result sets (jobs, attempts); membership is by the attempt's ClaimedByDaemonId.
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
