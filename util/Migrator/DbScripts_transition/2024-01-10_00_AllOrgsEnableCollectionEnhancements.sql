-- This script will enable collection enhancements for all organizations.

-- Step 1: Insert into a temporary table with an additional column for batch processing, update 50 k at a time
SELECT [Id] AS [OrganizationId], CAST(ROW_NUMBER() OVER(ORDER BY [Id]) / 50000 AS INT) AS Batch
INTO #TempOrg
FROM [dbo].[Organization];
-- WHERE [FlexibleCollections] = 0;

-- Step 2: Get the maximum batch number
DECLARE @MaxBatch INT = (SELECT MAX(Batch) FROM #TempOrg);
DECLARE @CurrentBatch INT = 0;

-- Step 3: Process each batch
WHILE @CurrentBatch <= @MaxBatch
BEGIN
    -- Execute the stored procedure for each OrganizationId in the current batch
    DECLARE @OrganizationId UNIQUEIDENTIFIER;

    DECLARE OrgCursor CURSOR FOR
    SELECT [OrganizationId]
    FROM #TempOrg
    WHERE [Batch] = @CurrentBatch;

    OPEN OrgCursor;

    FETCH NEXT FROM OrgCursor INTO @OrganizationId;

    WHILE @@FETCH_STATUS = 0
    BEGIN
        -- Execute the stored procedure for the current OrganizationId
        EXEC [dbo].[Organization_EnableCollectionEnhancements] @OrganizationId;

        FETCH NEXT FROM OrgCursor INTO @OrganizationId;
    END;

    CLOSE OrgCursor;
    DEALLOCATE OrgCursor;

    -- Move to the next batch
    SET @CurrentBatch = @CurrentBatch + 1;
END;

-- Step 4: Drop the temporary table
DROP TABLE #TempOrg;
