CREATE PROCEDURE [dbo].[PamRotationJob_Create]
    @Id UNIQUEIDENTIFIER,
    @RotationConfigId UNIQUEIDENTIFIER,
    @Source TINYINT,
    @Status TINYINT,
    @ClaimedByDaemonId UNIQUEIDENTIFIER = NULL,
    @ClaimedAt DATETIME2(7) = NULL,
    @CreationDate DATETIME2(7),
    @NextClaimableAt DATETIME2(7),
    @ExpiresAt DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON
    -- IPamRotationJobRepository.CreateGuardedAsync passes an already fully-populated PamRotationJob (Status =
    -- Pending, claim fields null, NextClaimableAt/ExpiresAt already computed by the caller) -- this sproc only
    -- re-validates can_offer's eligibility half and the AtMostOneActiveJobPerConfig guard before inserting it as-is
    -- (spec OfferRotation's single creation point). An explicit transaction is required so the range lock below is
    -- held until the INSERT commits; XACT_ABORT guarantees rollback (and a clean pooled connection) on any error.
    SET XACT_ABORT ON

    BEGIN TRANSACTION

    -- can_offer's eligibility half, re-checked here (not just by the caller) so a config disabled or a target
    -- disabled/switched to Manual between the caller's read and this write cannot mint a job. Outcome -1
    -- (ConfigNotOfferable) is distinct from the active-job conflict (0, ActiveJobExists) so the caller can tell
    -- "not offerable" apart from "already has one".
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

    -- AtMostOneActiveJobPerConfig. The UPDLOCK, HOLDLOCK range lock on [IX_PamRotationJob_RotationConfigId_Status] is
    -- held for the life of this transaction, so a concurrent creation attempt for the same config blocks here until
    -- this transaction commits, then sees the new job and is rejected.
    IF EXISTS (
        SELECT 1
        FROM [dbo].[PamRotationJob] WITH (UPDLOCK, HOLDLOCK)
        WHERE [RotationConfigId] = @RotationConfigId
            AND [Status] IN (0, 1) -- Pending, Claimed
    )
    BEGIN
        ROLLBACK TRANSACTION
        SELECT 0 -- ActiveJobExists
        RETURN
    END

    INSERT INTO [dbo].[PamRotationJob]
    (
        [Id], [RotationConfigId], [Source], [Status], [ClaimedByDaemonId], [ClaimedAt],
        [CreationDate], [NextClaimableAt], [ExpiresAt]
    )
    VALUES
    (
        @Id, @RotationConfigId, @Source, @Status, @ClaimedByDaemonId, @ClaimedAt,
        @CreationDate, @NextClaimableAt, @ExpiresAt
    )

    COMMIT TRANSACTION

    SELECT 1 -- Created
END
