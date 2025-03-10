UPDATE
    [dbo].[Organization]
SET
    [MaxStorageGb] = ISNULL([MaxStorageGb], 1)
WHERE
    [MaxStorageGb] IS NULL
    AND [PlanType] NOT IN (
        0, --Free
        6 --Custom
    )
