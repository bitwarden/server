CREATE PROCEDURE [dbo].[DataMigrationState_ReadManyByName]
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
