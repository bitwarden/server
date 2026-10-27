CREATE PROCEDURE [dbo].[PamRotationJob_ReadManyByConfigId]
    @RotationConfigId UNIQUEIDENTIFIER,
    @Limit INT,
    @Since DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    -- Two result sets let the caller zip jobs to attempts, grouped by JobId.
    SELECT TOP (@Limit)
        *
    INTO
        #Jobs
    FROM
        [dbo].[PamRotationJobView]
    WHERE
        [RotationConfigId] = @RotationConfigId
        AND [CreationDate] >= @Since
    ORDER BY
        [CreationDate] DESC

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
    ORDER BY
        A.[JobId],
        A.[CreationDate] ASC

    DROP TABLE #Jobs
END
