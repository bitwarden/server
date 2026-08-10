CREATE PROCEDURE [dbo].[DataMigrationState_TryClaim]
    @Name VARCHAR(100),
    @LeaseOwner VARCHAR(100),
    @LeaseExpiresDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    -- Work-queue claim: READPAST lets concurrent claimers skip one another's locked rows instead
    -- of serializing behind them; losers observe zero rows and exit.
    UPDATE TOP (1) [dbo].[DataMigrationState] WITH (READPAST, ROWLOCK)
    SET
        [LeaseOwner] = @LeaseOwner,
        [LeaseExpiresDate] = @LeaseExpiresDate,
        [RevisionDate] = GETUTCDATE()
    OUTPUT
        INSERTED.[Partition],
        INSERTED.[RangeStart],
        INSERTED.[RangeEnd],
        INSERTED.[Cursor],
        INSERTED.[TotalRows],
        INSERTED.[RowsScanned],
        INSERTED.[RowsConverted],
        INSERTED.[RowsSkippedByRace],
        INSERTED.[RowsFailed],
        INSERTED.[StartedDate]
    WHERE
        [Name] = @Name
        AND [CompletedDate] IS NULL
        AND ([LeaseOwner] IS NULL OR [LeaseExpiresDate] < GETUTCDATE())
END
