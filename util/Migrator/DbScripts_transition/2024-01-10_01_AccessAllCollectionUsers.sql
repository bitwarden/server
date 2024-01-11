-- Step 1: Insert into a temporary table with an additional column for batch processing, update 50 k at a time
SELECT [Id] AS [OrganizationUserId], [OrganizationId], CAST(ROW_NUMBER() OVER(ORDER BY [Id]) / 50000 AS INT) AS Batch
INTO #TempOrgUser
FROM [dbo].[OrganizationUser]
WHERE [AccessAll] = 1;

-- Step 2: Get the maximum batch number
DECLARE @MaxBatch INT = (SELECT MAX(Batch) FROM #TempOrgUser);
DECLARE @CurrentBatch INT = 0;

-- Step 3: Process each batch
WHILE @CurrentBatch <= @MaxBatch
BEGIN
    -- Update existing rows in [dbo].[CollectionUser]
    UPDATE target
    SET
        target.[ReadOnly] = 0,
        target.[HidePasswords] = 0,
        target.[Manage] = 0
    FROM [dbo].[CollectionUser] AS target
            INNER JOIN (
                SELECT C.[Id] AS [CollectionId], T.[OrganizationUserId]
                FROM [dbo].[Collection] C
                INNER JOIN #TempOrgUser T ON C.[OrganizationId] = T.[OrganizationId] AND T.Batch = @CurrentBatch
            ) AS source
    ON target.[CollectionId] = source.[CollectionId] AND target.[OrganizationUserId] = source.[OrganizationUserId];

    -- Insert new rows into [dbo].[CollectionUser]
    INSERT INTO [dbo].[CollectionUser] ([CollectionId], [OrganizationUserId], [ReadOnly], [HidePasswords], [Manage])
    SELECT source.[CollectionId], source.[OrganizationUserId], 0, 0, 0
    FROM (
             SELECT C.[Id] AS [CollectionId], T.[OrganizationUserId]
             FROM [dbo].[Collection] C
                 INNER JOIN #TempOrgUser T ON C.[OrganizationId] = T.[OrganizationId] AND T.Batch = @CurrentBatch
         ) AS source
             LEFT JOIN [dbo].[CollectionUser] AS target
    ON target.[CollectionId] = source.[CollectionId] AND target.[OrganizationUserId] = source.[OrganizationUserId]
    WHERE target.[CollectionId] IS NULL;

    -- Move to the next batch
    SET @CurrentBatch = @CurrentBatch + 1;
END;

-- Step 4: Update [dbo].[OrganizationUser] to clear AccessAll flag
UPDATE OU
SET [AccessAll] = 0
FROM [dbo].[OrganizationUser] OU
INNER JOIN #TempOrgUser T ON OU.[Id] = T.[OrganizationUserId]

-- Step 5: Bump the account revision date for each unique OrganizationUserId in #TempOrgUser
DECLARE @OrganizationUserId UNIQUEIDENTIFIER

DECLARE OrgUserIdCursor CURSOR FOR
SELECT DISTINCT [OrganizationUserId]
FROM #TempOrgUser

OPEN OrgUserIdCursor
FETCH NEXT FROM OrgUserIdCursor INTO @OrganizationUserId

WHILE (@@FETCH_STATUS = 0)
BEGIN
    -- Execute the stored procedure for the current OrganizationUserId
    EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationUserId] @OrganizationUserId

    -- Fetch the next OrganizationUserId
    FETCH NEXT FROM OrgUserIdCursor INTO @OrganizationUserId
END

CLOSE OrgUserIdCursor
DEALLOCATE OrgUserIdCursor;

-- Step 6: Drop the temporary table
DROP TABLE #TempOrgUser;
