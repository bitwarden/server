-- Step 1: Retrieve Groups with [AccessAll] permission
SELECT [Id] AS [GroupId], [OrganizationId]
INTO #TempGroup
FROM [dbo].[Group]
WHERE [AccessAll] = 1;

-- Step 2: Declare variables for GroupId and OrganizationId
DECLARE @GroupId UNIQUEIDENTIFIER;
DECLARE @OrganizationId UNIQUEIDENTIFIER;

-- Step 3: Create a cursor to iterate through the results of the temporary table
DECLARE GroupCursor CURSOR FOR
SELECT [GroupId], [OrganizationId]
FROM #TempGroup;

OPEN GroupCursor;

-- Step 4: Loop through the groups
FETCH NEXT FROM GroupCursor INTO @GroupId, @OrganizationId;

WHILE @@FETCH_STATUS = 0
BEGIN
    -- Step 5: Use MERGE to insert or update into [dbo].[CollectionGroup] for each [dbo].[Collection] entry
MERGE INTO [dbo].[CollectionGroup] AS target
    USING (SELECT C.[Id] AS [CollectionId], @GroupId AS [GroupId] FROM [dbo].[Collection] C WHERE C.[OrganizationId] = @OrganizationId) AS source
    ON (target.[CollectionId] = source.[CollectionId] AND target.[GroupId] = source.[GroupId])
    WHEN MATCHED THEN
UPDATE SET
    target.[ReadOnly] = 0,
    target.[HidePasswords] = 0,
    target.[Manage] = 1
    WHEN NOT MATCHED THEN
INSERT ([CollectionId], [GroupId], [ReadOnly], [HidePasswords], [Manage])
VALUES (source.[CollectionId], source.[GroupId], 0, 0, 1);

-- Step 6: Fetch the next GroupId and OrganizationId
FETCH NEXT FROM GroupCursor INTO @GroupId, @OrganizationId;
END;

-- Step 7: Close and deallocate the cursor
CLOSE GroupCursor;
DEALLOCATE GroupCursor;

-- Step 8: Drop the temporary table
DROP TABLE #TempGroup;
