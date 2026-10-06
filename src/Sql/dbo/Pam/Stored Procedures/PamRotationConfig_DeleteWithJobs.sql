CREATE PROCEDURE [dbo].[PamRotationConfig_DeleteWithJobs]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON
    -- Cascade: hard-deletes attempts, then jobs, then config, since both FKs are NO ACTION. The timeout journal
    -- cascades from the jobs on its own.
    SET XACT_ABORT ON

    BEGIN TRANSACTION

    -- Re-checks under PamRotationJob_Create's range lock so a mid-window claim can't be hard-deleted, nor a timeout
    -- the sweep has yet to record.
    IF EXISTS (
        SELECT 1
        FROM [dbo].[PamRotationJob] J WITH (UPDLOCK, HOLDLOCK)
        WHERE J.[RotationConfigId] = @Id
            AND J.[Action] IN (0, 1) -- None, Claimed
            AND NOT EXISTS (
                SELECT 1
                FROM [dbo].[PamRotationJobTimeoutSweep] S
                WHERE S.[RotationJobId] = J.[Id]
            )
    )
    BEGIN
        ROLLBACK TRANSACTION
        SELECT 0 -- ActiveJobExists
        RETURN
    END

    DELETE A
    FROM [dbo].[PamRotationAttempt] A
    INNER JOIN [dbo].[PamRotationJob] J ON J.[Id] = A.[JobId]
    WHERE J.[RotationConfigId] = @Id

    DELETE FROM [dbo].[PamRotationJob]
    WHERE [RotationConfigId] = @Id

    DELETE FROM [dbo].[PamRotationConfig]
    WHERE [Id] = @Id

    COMMIT TRANSACTION

    SELECT 1 -- Deleted
END
