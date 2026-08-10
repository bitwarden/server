-- Framework state table for gradual, resumable data migrations (BaseMigrationJob):
-- one row per (migration, partition), leased via conditional updates, checkpointed with an
-- owner fence.

IF OBJECT_ID('[dbo].[DataMigrationState]') IS NULL
BEGIN
    CREATE TABLE [dbo].[DataMigrationState] (
        [Id]                UNIQUEIDENTIFIER NOT NULL,
        [Name]              VARCHAR(100)     NOT NULL,
        [Partition]         INT              NOT NULL,
        [RangeStart]        NVARCHAR(300)    NULL,
        [RangeEnd]          NVARCHAR(300)    NULL,
        [Cursor]            NVARCHAR(300)    NULL,
        [TotalRows]         BIGINT           NOT NULL CONSTRAINT [DF_DataMigrationState_TotalRows] DEFAULT (0),
        [RowsScanned]       BIGINT           NOT NULL CONSTRAINT [DF_DataMigrationState_RowsScanned] DEFAULT (0),
        [RowsConverted]     BIGINT           NOT NULL CONSTRAINT [DF_DataMigrationState_RowsConverted] DEFAULT (0),
        [RowsSkippedByRace] BIGINT           NOT NULL CONSTRAINT [DF_DataMigrationState_RowsSkippedByRace] DEFAULT (0),
        [RowsFailed]        BIGINT           NOT NULL CONSTRAINT [DF_DataMigrationState_RowsFailed] DEFAULT (0),
        [LeaseOwner]        VARCHAR(100)     NULL,
        [LeaseExpiresDate]  DATETIME2(7)     NULL,
        [StartedDate]       DATETIME2(7)     NULL,
        [CompletedDate]     DATETIME2(7)     NULL,
        [CreationDate]      DATETIME2(7)     NOT NULL,
        [RevisionDate]      DATETIME2(7)     NOT NULL,
        CONSTRAINT [PK_DataMigrationState] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [IX_DataMigrationState_NamePartition] UNIQUE NONCLUSTERED ([Name] ASC, [Partition] ASC)
    );
END
GO

CREATE OR ALTER PROCEDURE [dbo].[DataMigrationState_Initialize]
    @Name VARCHAR(100),
    @Partitions NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON

    -- Single INSERT statement = atomic: when two instances race to initialize, the loser's
    -- statement fails whole on the unique (Name, Partition) index and no partial boundary set can
    -- ever be observed.
    INSERT INTO [dbo].[DataMigrationState]
    (
        [Id],
        [Name],
        [Partition],
        [RangeStart],
        [RangeEnd],
        [TotalRows],
        [CreationDate],
        [RevisionDate]
    )
    SELECT
        P.[Id],
        @Name,
        P.[Partition],
        P.[RangeStart],
        P.[RangeEnd],
        ISNULL(P.[TotalRows], 0),
        GETUTCDATE(),
        GETUTCDATE()
    FROM OPENJSON(@Partitions) WITH (
        [Id] UNIQUEIDENTIFIER '$.id',
        [Partition] INT '$.partition',
        [RangeStart] NVARCHAR(300) '$.rangeStart',
        [RangeEnd] NVARCHAR(300) '$.rangeEnd',
        [TotalRows] BIGINT '$.totalRows'
    ) P
END
GO

CREATE OR ALTER PROCEDURE [dbo].[DataMigrationState_TryClaim]
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
GO

CREATE OR ALTER PROCEDURE [dbo].[DataMigrationState_Checkpoint]
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
GO

CREATE OR ALTER PROCEDURE [dbo].[DataMigrationState_Release]
    @Name VARCHAR(100),
    @Partition INT,
    @LeaseOwner VARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON

    -- Fenced like the checkpoint: releasing a lease we no longer hold is a no-op.
    UPDATE [dbo].[DataMigrationState]
    SET
        [LeaseOwner] = NULL,
        [LeaseExpiresDate] = NULL,
        [RevisionDate] = GETUTCDATE()
    WHERE
        [Name] = @Name
        AND [Partition] = @Partition
        AND [LeaseOwner] = @LeaseOwner
END
GO

CREATE OR ALTER PROCEDURE [dbo].[DataMigrationState_ReadManyByName]
    @Name VARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON

    -- Plain read, no lease predicate: the pending-rows tally must see leased partitions too.
    SELECT
        [Partition],
        [TotalRows],
        [RowsScanned],
        CAST(CASE WHEN [CompletedDate] IS NOT NULL THEN 1 ELSE 0 END AS BIT) AS [Completed]
    FROM [dbo].[DataMigrationState]
    WHERE [Name] = @Name
    ORDER BY [Partition] ASC
END
GO

CREATE OR ALTER PROCEDURE [dbo].[DataMigrationState_ReadCountByName]
    @Name VARCHAR(100),
    @IncompleteOnly BIT
AS
BEGIN
    SET NOCOUNT ON

    SELECT COUNT(*)
    FROM [dbo].[DataMigrationState]
    WHERE
        [Name] = @Name
        AND (@IncompleteOnly = 0 OR [CompletedDate] IS NULL)
END
GO
