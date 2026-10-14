CREATE PROCEDURE [dbo].[PamRotationJob_ReadManyByConfigId]
    @RotationConfigId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    -- Two result sets let the caller zip jobs to attempts, grouped by JobId.
    SELECT
        *
    FROM
        [dbo].[PamRotationJobView]
    WHERE
        [RotationConfigId] = @RotationConfigId
    ORDER BY
        [CreationDate] DESC

    SELECT
        A.*
    FROM
        [dbo].[PamRotationAttemptView] A
    INNER JOIN
        [dbo].[PamRotationJobView] J ON J.[Id] = A.[JobId]
    WHERE
        J.[RotationConfigId] = @RotationConfigId
    ORDER BY
        A.[JobId],
        A.[CreationDate] ASC
END
