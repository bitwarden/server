CREATE PROCEDURE [dbo].[PamRotationConfig_DeleteWithJobs]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON
    -- Cascade: hard-deletes attempts, then jobs, then config, since both FKs are NO ACTION.
    SET XACT_ABORT ON

    BEGIN TRANSACTION

    -- Re-checks under PamRotationJob_Create's range lock so a mid-window claim can't be hard-deleted.
    IF EXISTS (
        SELECT 1
        FROM [dbo].[PamRotationJob] WITH (UPDLOCK, HOLDLOCK)
        WHERE [RotationConfigId] = @Id
            AND [Status] IN (0, 1) -- Pending, Claimed
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
