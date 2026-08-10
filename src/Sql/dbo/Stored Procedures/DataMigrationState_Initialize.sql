CREATE PROCEDURE [dbo].[DataMigrationState_Initialize]
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
