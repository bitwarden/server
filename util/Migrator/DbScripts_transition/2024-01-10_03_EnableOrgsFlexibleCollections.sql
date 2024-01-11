-- Step 1: Insert into a temporary table with an additional column for batch processing, update 50 k at a time
SELECT [Id] AS [OrganizationId], CAST(ROW_NUMBER() OVER(ORDER BY [Id]) / 50000 AS INT) AS Batch
INTO #TempOrg
FROM [dbo].[Organization]
WHERE [FlexibleCollections] = 0;

-- Step 2: Get the maximum batch number
DECLARE @MaxBatch INT = (SELECT MAX(Batch) FROM #TempOrg);
DECLARE @CurrentBatch INT = 0;

-- Step 3: Process each batch
WHILE @CurrentBatch <= @MaxBatch
BEGIN
    -- Update existing rows in [dbo].[CollectionUser]
    UPDATE target
    SET
        target.[FlexibleCollections] = 1
    FROM [dbo].[Organization] AS target
    INNER JOIN #TempOrg AS source
        ON target.[Id] = source.[OrganizationId]
    WHERE source.[Batch] = @CurrentBatch;

    -- Move to the next batch
    SET @CurrentBatch = @CurrentBatch + 1;
END;

-- Step 4: Drop the temporary table
DROP TABLE #TempOrg;
