CREATE PROCEDURE [dbo].[PamRotationJob_ReadManyByConfigId]
    @RotationConfigId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    -- The config detail page's attempt history, returned as two result sets so the caller can zip each job to its
    -- attempts (IPamRotationJobRepository.GetManyByConfigIdAsync, grouping the second set by JobId) without an N+1:
    --   1) every job for the config, newest first.
    --   2) every attempt belonging to those jobs, oldest-first within a job.
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
