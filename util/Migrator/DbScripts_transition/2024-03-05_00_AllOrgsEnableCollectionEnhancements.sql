-- This script will enable collection enhancements for organizations that don't have Collection Enhancements enabled.

DECLARE @Percentage FLOAT = 0.1; -- Define the percentage of rows to select (10% in this case)

-- Step 1: Insert into a temporary table, selecting a percentage of the rows for each distinct PlanType
SELECT [Id] AS [OrganizationId]
INTO #TempOrg
FROM (
    SELECT [Id], [PlanType], ROW_NUMBER() OVER(PARTITION BY [PlanType] ORDER BY NEWID()) AS RowNum
    FROM [dbo].[Organization]
    ) AS Subquery
WHERE Subquery.RowNum <= (
    SELECT ROUND(@Percentage * COUNT(*), 0)
    FROM [dbo].[Organization]
    WHERE [PlanType] = Subquery.[PlanType]
    );

-- Step 2: Execute the stored procedure for each OrganizationId
DECLARE @OrganizationId UNIQUEIDENTIFIER;

DECLARE OrgCursor CURSOR FOR
SELECT [OrganizationId]
FROM #TempOrg;

OPEN OrgCursor;

FETCH NEXT FROM OrgCursor INTO @OrganizationId;

WHILE (@@FETCH_STATUS = 0)
BEGIN
    -- Backup the necessary data to rollback if needed
    EXEC [dbo].[Organization_BackupPreCollectionEnhancementsData] @OrganizationId;

    -- Execute the stored procedure for the current OrganizationId
    EXEC [dbo].[Organization_EnableCollectionEnhancements] @OrganizationId;

    -- Update the Organization to set FlexibleCollections = 1
    UPDATE [vault_dev].[dbo].[Organization]
    SET [FlexibleCollections] = 1
    WHERE [Id] = @OrganizationId;

    FETCH NEXT FROM OrgCursor INTO @OrganizationId;
END;

CLOSE OrgCursor;
DEALLOCATE OrgCursor;

-- Step 3: Drop the temporary table
DROP TABLE #TempOrg;
