-- Stops the lease expiry sweep deadlocking with lease revokes, extensions and organization deletes: the sweep now
-- stamps due leases one at a time by [Id], and an index on [OrganizationId] keeps the organization delete cascade to
-- that organization's leases.
IF NOT EXISTS (
    SELECT
        1
    FROM
        sys.indexes
    WHERE
        [name] = 'IX_AccessLease_OrganizationId'
        AND object_id = OBJECT_ID('[dbo].[AccessLease]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AccessLease_OrganizationId]
    ON [dbo].[AccessLease] ([OrganizationId] ASC);
END
GO

CREATE OR ALTER PROCEDURE [dbo].[PamLeaseExpirySweep_CreateManyDue]
    @Now DATETIME2(7),
    @Since DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON
    -- XACT_ABORT rolls back errors that skip CATCH (batch-aborting errors, client timeouts).
    SET XACT_ABORT ON

    -- Expiry is derived, not stored; stamping [SweptDate] decides which run owns a lease.
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
    DECLARE @Id UNIQUEIDENTIFIER

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

    DECLARE [DueCursor] CURSOR LOCAL FAST_FORWARD FOR
        SELECT [Id] FROM @Due ORDER BY [Id]

    BEGIN TRY
        -- All or nothing: a lease stamped but never returned would never have its expiry processed.
        BEGIN TRANSACTION

        OPEN [DueCursor]
        FETCH NEXT FROM [DueCursor] INTO @Id
        WHILE @@FETCH_STATUS = 0
        BEGIN
            -- One lease by [Id], so the row is locked before its index entries, in the same order as the writers
            -- that revoke or extend a lease. Re-checked under the row lock, so a lease that a concurrent sweep
            -- stamped first, or that a revoke or extension changed after the read, is left out.
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
                [Id] = @Id
                AND [Action] = 0
                AND [SweptDate] IS NULL
                AND [NotAfter] <= @Now

            FETCH NEXT FROM [DueCursor] INTO @Id
        END
        CLOSE [DueCursor]
        DEALLOCATE [DueCursor]

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
GO
