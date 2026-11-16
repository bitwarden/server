CREATE PROCEDURE [dbo].[PamLeaseExpirySweep_CreateManyDue]
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
