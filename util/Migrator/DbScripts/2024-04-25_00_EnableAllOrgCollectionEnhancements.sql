-- This script will enable collection enhancements for organizations that don't have Collection Enhancements enabled.

-- Step 1: Create a temporary table to store the Organizations with FlexibleCollections = 0
SELECT [Id] AS [OrganizationId]
INTO #TempOrg
FROM [dbo].[Organization]
WHERE [FlexibleCollections] = 0

-- Step 2: Execute the stored procedure for each OrganizationId
DECLARE @OrganizationId UNIQUEIDENTIFIER;

DECLARE OrgCursor CURSOR FOR
SELECT [OrganizationId]
FROM #TempOrg;

OPEN OrgCursor;

FETCH NEXT FROM OrgCursor INTO @OrganizationId;

WHILE (@@FETCH_STATUS = 0)
BEGIN
    -- Execute the stored procedure for the current OrganizationId
    EXEC [dbo].[Organization_EnableCollectionEnhancements] @OrganizationId;

    -- Update the Organization to set FlexibleCollections = 1
    UPDATE [dbo].[Organization]
    SET [FlexibleCollections] = 1
    WHERE [Id] = @OrganizationId;

    FETCH NEXT FROM OrgCursor INTO @OrganizationId;
END;

CLOSE OrgCursor;
DEALLOCATE OrgCursor;

-- Step 3: Drop the temporary table
DROP TABLE #TempOrg;
