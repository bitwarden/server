CREATE PROCEDURE [dbo].[PamRotationConfig_DeleteWithJobs]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON
    -- Hard-deletes the config; its jobs, their attempts and the timeout journal go with it through the FK cascade.
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

    DELETE FROM [dbo].[PamRotationConfig]
    WHERE [Id] = @Id

    COMMIT TRANSACTION

    SELECT 1 -- Deleted
END
