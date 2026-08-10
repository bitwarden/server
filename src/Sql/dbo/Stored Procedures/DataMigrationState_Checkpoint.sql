CREATE PROCEDURE [dbo].[DataMigrationState_Checkpoint]
    @Name VARCHAR(100),
    @Partition INT,
    @LeaseOwner VARCHAR(100),
    @LeaseExpiresDate DATETIME2(7),
    @Cursor NVARCHAR(300),
    @RowsScanned BIGINT,
    @RowsConverted BIGINT,
    @RowsSkippedByRace BIGINT,
    @RowsFailed BIGINT,
    @StartedDate DATETIME2(7),
    @CompletedDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    -- Fenced write: the LeaseOwner predicate prevents a worker that lost its lease (paused,
    -- partitioned, past expiry) from clobbering the new owner's cursor. Zero rows = fenced out.
    -- Also renews the lease: renewal rides the per-batch checkpoint, so a processor holds its
    -- partition while it makes progress and a stalled one stops renewing and expires.
    UPDATE [dbo].[DataMigrationState]
    SET
        [LeaseExpiresDate] = @LeaseExpiresDate,
        [Cursor] = @Cursor,
        [RowsScanned] = @RowsScanned,
        [RowsConverted] = @RowsConverted,
        [RowsSkippedByRace] = @RowsSkippedByRace,
        [RowsFailed] = @RowsFailed,
        [StartedDate] = @StartedDate,
        [CompletedDate] = @CompletedDate,
        [RevisionDate] = GETUTCDATE()
    WHERE
        [Name] = @Name
        AND [Partition] = @Partition
        AND [LeaseOwner] = @LeaseOwner

    SELECT @@ROWCOUNT
END
