CREATE PROCEDURE [dbo].[PamRotationConfig_UpdateManualObligationOnAccessEnd]
    @Id UNIQUEIDENTIFIER,
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON
    SET XACT_ABORT ON

    -- Spec RaiseManualObligationOnAccessEnd. Clearing the pending access end in the same transaction means a sweep
    -- retry racing the inline trigger raises the obligation once.
    BEGIN TRY
        BEGIN TRANSACTION

        DELETE
        FROM
            [dbo].[PamPendingAccessEnd]
        WHERE
            [RotationConfigId] = @Id

        IF @@ROWCOUNT = 0
        BEGIN
            ROLLBACK TRANSACTION
            SELECT 0 -- NothingPending
            RETURN
        END

        UPDATE
            [dbo].[PamRotationConfig]
        SET
            [NextRotationDate] = @Now,
            [RevisionDate] = @Now
        WHERE
            [Id] = @Id

        COMMIT TRANSACTION

        SELECT 1 -- Raised
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
