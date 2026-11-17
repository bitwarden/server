-- Adds PamPendingAccessEnd, so an access-end rotation trigger that fails after the lease ends is retried by the
-- rotation sweep instead of lost. Revoke and the expiry sweep now record it in the transaction that ends the lease.

IF OBJECT_ID('[dbo].[PamPendingAccessEnd]') IS NULL
BEGIN
    CREATE TABLE [dbo].[PamPendingAccessEnd] (
        [AccessLeaseId]     UNIQUEIDENTIFIER    NOT NULL,
        [RotationConfigId]  UNIQUEIDENTIFIER    NOT NULL,
        [CreationDate]      DATETIME2(7)        NOT NULL,
        CONSTRAINT [PK_PamPendingAccessEnd] PRIMARY KEY CLUSTERED ([AccessLeaseId] ASC),
        CONSTRAINT [FK_PamPendingAccessEnd_PamRotationConfig] FOREIGN KEY ([RotationConfigId]) REFERENCES [dbo].[PamRotationConfig] ([Id]) ON DELETE CASCADE
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'IX_PamPendingAccessEnd_RotationConfigId' AND object_id = OBJECT_ID('[dbo].[PamPendingAccessEnd]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_PamPendingAccessEnd_RotationConfigId]
        ON [dbo].[PamPendingAccessEnd] ([RotationConfigId] ASC);
END
GO

CREATE OR ALTER PROCEDURE [dbo].[AccessLease_UpdateRevoked]
    @AccessLeaseId UNIQUEIDENTIFIER,
    @Action TINYINT,
    @RevokedBy UNIQUEIDENTIFIER,
    @AccessDecisionId UNIQUEIDENTIFIER,
    @Reason NVARCHAR(MAX) = NULL,
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON
    -- XACT_ABORT rolls back errors that skip CATCH (batch-aborting errors, client timeouts).
    SET XACT_ABORT ON

    -- Ends a running lease (2 Revoked or 3 Cancelled), idempotently; feeds a Deny AccessDecision.
    DECLARE @Ended TABLE ([AccessRequestId] UNIQUEIDENTIFIER, [CipherId] UNIQUEIDENTIFIER)

    BEGIN TRY
        BEGIN TRANSACTION

        UPDATE [dbo].[AccessLease]
        SET [Action] = @Action,
            [RevokedDate] = @Now,
            [RevokedBy] = @RevokedBy
        OUTPUT INSERTED.[AccessRequestId], INSERTED.[CipherId] INTO @Ended
        WHERE [Id] = @AccessLeaseId
            AND [Action] = 0 -- None (no early end)
            AND [NotAfter] > @Now

        INSERT INTO [dbo].[AccessDecision]
        (
            [Id], [AccessRequestId], [DeciderKind], [ApproverId], [ConditionKind],
            [Verdict], [Comment], [EvaluationContext], [CreationDate]
        )
        SELECT
            @AccessDecisionId, E.[AccessRequestId], 1 /* Human */, @RevokedBy, NULL,
            0 /* Deny */, @Reason, NULL, @Now
        FROM @Ended E

        -- The access-end trigger runs after commit; this row lets the rotation sweep retry it if it fails.
        INSERT INTO [dbo].[PamPendingAccessEnd]
        (
            [AccessLeaseId],
            [RotationConfigId],
            [CreationDate]
        )
        SELECT
            @AccessLeaseId,
            C.[Id],
            @Now
        FROM
            @Ended E
        INNER JOIN
            [dbo].[PamRotationConfig] C ON C.[CipherId] = E.[CipherId]
        WHERE
            C.[RotateOnAccessEnd] = 1
            AND C.[Enabled] = 1

        COMMIT TRANSACTION
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

CREATE OR ALTER PROCEDURE [dbo].[PamLeaseExpirySweep_CreateManyDue]
    @Now DATETIME2(7),
    @Since DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON
    SET XACT_ABORT ON

    -- Expiry is derived, not stored; PamLeaseExpirySweep's INSERT decides which run owns a lease.
    -- UPDLOCK/HOLDLOCK serializes concurrent sweeps; a loser re-checks after commit and skips it.
    DECLARE @Due TABLE ([AccessLeaseId] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY)

    BEGIN TRY
        BEGIN TRANSACTION

        INSERT INTO [dbo].[PamLeaseExpirySweep]
        (
            [AccessLeaseId],
            [SweptDate]
        )
        OUTPUT
            inserted.[AccessLeaseId]
        INTO @Due
        SELECT
            AL.[Id],
            @Now
        FROM
            [dbo].[AccessLease] AL
        WHERE
            AL.[Action] = 0 -- None: no early end recorded, so the closed window is a natural expiry
            AND AL.[NotAfter] <= @Now
            AND AL.[NotAfter] > @Since -- keeps the read from growing with lease history
            AND NOT EXISTS (
                SELECT
                    1
                FROM
                    [dbo].[PamLeaseExpirySweep] S WITH (UPDLOCK, HOLDLOCK)
                WHERE
                    S.[AccessLeaseId] = AL.[Id]
            )

        -- The caller runs the access-end trigger after commit; this row lets the rotation sweep retry it if it fails.
        INSERT INTO [dbo].[PamPendingAccessEnd]
        (
            [AccessLeaseId],
            [RotationConfigId],
            [CreationDate]
        )
        SELECT
            AL.[Id],
            C.[Id],
            @Now
        FROM
            @Due D
        INNER JOIN
            [dbo].[AccessLease] AL ON AL.[Id] = D.[AccessLeaseId]
        INNER JOIN
            [dbo].[PamRotationConfig] C ON C.[CipherId] = AL.[CipherId]
        WHERE
            C.[RotateOnAccessEnd] = 1
            AND C.[Enabled] = 1

        COMMIT TRANSACTION
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;
        THROW;
    END CATCH

    -- No join needed; the caller audits/triggers off the lease row itself.
    SELECT
        AL.[Id],
        AL.[OrganizationId],
        AL.[CollectionId],
        AL.[CipherId],
        AL.[RequesterId],
        AL.[NotBefore],
        AL.[NotAfter]
    FROM
        [dbo].[AccessLease] AL
    INNER JOIN
        @Due D ON D.[AccessLeaseId] = AL.[Id]
END
GO

CREATE OR ALTER PROCEDURE [dbo].[PamRotationConfig_ReadManyPendingAccessEnd]
    @CreatedBefore DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    -- The sweep's access-end phase. Newer rows are left to the trigger the lease end runs inline.
    SELECT
        C.*
    FROM
        [dbo].[PamRotationConfigView] C
    WHERE
        EXISTS (
            SELECT
                1
            FROM
                [dbo].[PamPendingAccessEnd] P
            WHERE
                P.[RotationConfigId] = C.[Id]
                AND P.[CreationDate] <= @CreatedBefore
        )
END
GO

CREATE OR ALTER PROCEDURE [dbo].[PamPendingAccessEnd_DeleteByRotationConfigId]
    @RotationConfigId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DELETE
    FROM
        [dbo].[PamPendingAccessEnd]
    WHERE
        [RotationConfigId] = @RotationConfigId
END
GO

CREATE OR ALTER PROCEDURE [dbo].[PamRotationConfig_UpdateManualObligationOnAccessEnd]
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
GO
