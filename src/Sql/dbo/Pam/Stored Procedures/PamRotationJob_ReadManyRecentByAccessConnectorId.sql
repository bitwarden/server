CREATE PROCEDURE [dbo].[PamRotationJob_ReadManyRecentByAccessConnectorId]
    @AccessConnectorId UNIQUEIDENTIFIER,
    @Limit INT,
    @Since DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    -- Two result sets (jobs, attempts); membership is by an attempt of this access connector since @Since.
    SELECT TOP (@Limit)
        J.*
    INTO
        #Jobs
    FROM
        [dbo].[PamRotationJobView] J
    WHERE
        EXISTS (
            SELECT
                1
            FROM
                [dbo].[PamRotationAttemptView] A
            WHERE
                A.[JobId] = J.[Id]
                AND A.[ClaimedByAccessConnectorId] = @AccessConnectorId
                AND A.[CreationDate] >= @Since
        )
    ORDER BY
        J.[CreationDate] DESC

    SELECT
        *
    FROM
        #Jobs
    ORDER BY
        [CreationDate] DESC

    SELECT
        A.*
    FROM
        [dbo].[PamRotationAttemptView] A
    INNER JOIN
        #Jobs J ON J.[Id] = A.[JobId]
    WHERE
        A.[ClaimedByAccessConnectorId] = @AccessConnectorId
    ORDER BY
        A.[JobId],
        A.[CreationDate] ASC

    DROP TABLE #Jobs
END
