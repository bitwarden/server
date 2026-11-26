CREATE PROCEDURE [dbo].[PamLeaseExpirySweep_CreateManyDue]
    @Now DATETIME2(7),
    @Since DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON
    SET XACT_ABORT ON

    -- Expiry is derived, not stored; stamping [SweptDate] decides which run owns a lease. A concurrent sweep blocks
    -- on the row lock and re-checks [SweptDate] IS NULL afterwards, so it skips what the winner stamped.
    DECLARE @Swept TABLE (
        [Id]                UNIQUEIDENTIFIER    NOT NULL PRIMARY KEY,
        [OrganizationId]    UNIQUEIDENTIFIER    NOT NULL,
        [CollectionId]      UNIQUEIDENTIFIER    NOT NULL,
        [CipherId]          UNIQUEIDENTIFIER    NOT NULL,
        [RequesterId]       UNIQUEIDENTIFIER    NOT NULL,
        [NotBefore]         DATETIME2(7)        NOT NULL,
        [NotAfter]          DATETIME2(7)        NOT NULL
    )

    BEGIN TRY
        BEGIN TRANSACTION

        UPDATE
            [dbo].[AccessLease]
        SET
            [SweptDate] = @Now
        OUTPUT
            inserted.[Id],
            inserted.[OrganizationId],
            inserted.[CollectionId],
            inserted.[CipherId],
            inserted.[RequesterId],
            inserted.[NotBefore],
            inserted.[NotAfter]
        INTO @Swept
        WHERE
            [Action] = 0 -- None: no early end recorded, so the closed window is a natural expiry
            AND [SweptDate] IS NULL
            AND [NotAfter] <= @Now
            AND [NotAfter] > @Since -- keeps the read from growing with lease history

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
