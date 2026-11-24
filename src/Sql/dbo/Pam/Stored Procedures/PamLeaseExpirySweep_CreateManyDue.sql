CREATE PROCEDURE [dbo].[PamLeaseExpirySweep_CreateManyDue]
    @Now DATETIME2(7),
    @Since DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON
    SET XACT_ABORT ON

    -- Expiry is derived, not stored; stamping [SweptDate] decides which run owns a lease. The due leases are read
    -- first and stamped by [Id], so the sweep locks each lease row before its index entries, in the same order as
    -- the writers that revoke or extend a lease.
    DECLARE @Due TABLE ([Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY)
    DECLARE @Swept TABLE (
        [Id]                UNIQUEIDENTIFIER    NOT NULL PRIMARY KEY,
        [OrganizationId]    UNIQUEIDENTIFIER    NOT NULL,
        [CollectionId]      UNIQUEIDENTIFIER    NOT NULL,
        [CipherId]          UNIQUEIDENTIFIER    NOT NULL,
        [RequesterId]       UNIQUEIDENTIFIER    NOT NULL,
        [NotBefore]         DATETIME2(7)        NOT NULL,
        [NotAfter]          DATETIME2(7)        NOT NULL
    )

    INSERT INTO @Due ([Id])
    SELECT
        [Id]
    FROM
        [dbo].[AccessLease]
    WHERE
        [Action] = 0 -- None: no early end recorded, so the closed window is a natural expiry
        AND [SweptDate] IS NULL
        AND [NotAfter] <= @Now
        AND [NotAfter] > @Since -- keeps the read from growing with lease history

    BEGIN TRY
        BEGIN TRANSACTION

        -- Re-checked under the row lock, so a lease that a concurrent sweep stamped first, or that a revoke or
        -- extension changed after the read, is left out.
        UPDATE
            AL
        SET
            AL.[SweptDate] = @Now
        OUTPUT
            inserted.[Id],
            inserted.[OrganizationId],
            inserted.[CollectionId],
            inserted.[CipherId],
            inserted.[RequesterId],
            inserted.[NotBefore],
            inserted.[NotAfter]
        INTO @Swept
        FROM
            [dbo].[AccessLease] AL
        INNER JOIN
            @Due D ON D.[Id] = AL.[Id]
        WHERE
            AL.[Action] = 0
            AND AL.[SweptDate] IS NULL
            AND AL.[NotAfter] <= @Now

        -- The caller runs the access-end trigger after commit; this row lets the rotation sweep retry it if it fails.
        INSERT INTO [dbo].[PamPendingAccessEnd]
        (
            [AccessLeaseId],
            [RotationConfigId],
            [CreationDate]
        )
        SELECT
            S.[Id],
            C.[Id],
            @Now
        FROM
            @Swept S
        INNER JOIN
            [dbo].[PamRotationConfig] C ON C.[CipherId] = S.[CipherId]
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
        [Id],
        [OrganizationId],
        [CollectionId],
        [CipherId],
        [RequesterId],
        [NotBefore],
        [NotAfter]
    FROM
        @Swept
END
