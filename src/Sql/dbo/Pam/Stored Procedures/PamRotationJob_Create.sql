CREATE PROCEDURE [dbo].[PamRotationJob_Create]
    @Id UNIQUEIDENTIFIER,
    @RotationConfigId UNIQUEIDENTIFIER,
    @Source TINYINT,
    @Action TINYINT,
    @ClaimedByAccessConnectorId UNIQUEIDENTIFIER = NULL,
    @ClaimedAt DATETIME2(7) = NULL,
    @CreationDate DATETIME2(7),
    @NextClaimableAt DATETIME2(7),
    @ExpiresAt DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON
    -- Caller passes an already-populated unclaimed job; this only re-validates eligibility and the guard.
    -- Holds the range lock until the INSERT commits.
    SET XACT_ABORT ON

    BEGIN TRANSACTION

    -- Re-checked so a config/target disabled between read and write can't mint a job.
    -- Outcome -1 (ConfigNotOfferable) is distinct from 0 (ActiveJobExists).
    IF NOT EXISTS (
        SELECT 1
        FROM [dbo].[PamRotationConfig] C WITH (UPDLOCK, HOLDLOCK)
        INNER JOIN [dbo].[PamTargetSystem] T ON T.[Id] = C.[TargetSystemId]
        WHERE C.[Id] = @RotationConfigId
            AND C.[Enabled] = 1
            AND T.[Method] = 0 -- Automatic
            AND T.[Status] = 0 -- Active
    )
    BEGIN
        ROLLBACK TRANSACTION
        SELECT -1 -- ConfigNotOfferable
        RETURN
    END

    -- AtMostOneActiveJobPerConfig: range lock holds for the transaction, blocking concurrent creation.
    -- A timed-out job holds its config until the timeout sweep records it, so the sweep's reschedule lands first.
    IF EXISTS (
        SELECT 1
        FROM [dbo].[PamRotationJob] J WITH (UPDLOCK, HOLDLOCK)
        WHERE J.[RotationConfigId] = @RotationConfigId
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

    INSERT INTO [dbo].[PamRotationJob]
    (
        [Id], [RotationConfigId], [Source], [Action], [ClaimedByAccessConnectorId], [ClaimedAt],
        [CreationDate], [NextClaimableAt], [ExpiresAt]
    )
    VALUES
    (
        @Id, @RotationConfigId, @Source, @Action, @ClaimedByAccessConnectorId, @ClaimedAt,
        @CreationDate, @NextClaimableAt, @ExpiresAt
    )

    COMMIT TRANSACTION

    SELECT 1 -- Created
END
