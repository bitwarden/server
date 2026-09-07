CREATE PROCEDURE [dbo].[PamRotationJob_ReadManyByConfigId]
    @RotationConfigId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    -- Two result sets let the caller zip jobs to attempts, grouped by JobId.
    SELECT *
    FROM [dbo].[PamRotationJob]
    WHERE [RotationConfigId] = @RotationConfigId
    ORDER BY [CreationDate] DESC

    SELECT A.*
    FROM [dbo].[PamRotationAttempt] A
    INNER JOIN [dbo].[PamRotationJob] J ON J.[Id] = A.[JobId]
    WHERE J.[RotationConfigId] = @RotationConfigId
    ORDER BY A.[JobId], A.[CreationDate] ASC
END
