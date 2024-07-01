UPDATE
    [dbo].[Organization]
SET
    [MaxStorageGb] = ISNULL([MaxStorageGb], 1),
    [Seats] = ISNULL([Seats], 1)
WHERE
    [MaxStorageGb] IS NULL
    OR [Seats] IS NULL;
