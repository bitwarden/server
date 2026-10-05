CREATE PROCEDURE [dbo].[PamRotationJob_ReadManyRecentByAccessConnectorId]
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
