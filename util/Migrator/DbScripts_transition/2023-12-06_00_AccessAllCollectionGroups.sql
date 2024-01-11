-- Step 1: Create a temporary table to store the Groups with AccessAll = 1
SELECT [Id] AS [GroupId], [OrganizationId]
INTO #TempGroup
FROM [dbo].[Group]
WHERE [AccessAll] = 1;

-- Step 2: Update existing rows in [dbo].[CollectionGroup]
UPDATE CG
SET
    CG.[ReadOnly] = 0,
    CG.[HidePasswords] = 0,
    CG.[Manage] = 0
    FROM [dbo].[CollectionGroup] CG
INNER JOIN [dbo].[Collection] C ON CG.[CollectionId] = C.[Id]
INNER JOIN #TempGroup TG ON CG.[GroupId] = TG.[GroupId]
WHERE C.[OrganizationId] = TG.[OrganizationId];

-- Step 3: Insert new rows into [dbo].[CollectionGroup]
INSERT INTO [dbo].[CollectionGroup] ([CollectionId], [GroupId], [ReadOnly], [HidePasswords], [Manage])
SELECT C.[Id], TG.[GroupId], 0, 0, 0
FROM [dbo].[Collection] C
    INNER JOIN #TempGroup TG ON C.[OrganizationId] = TG.[OrganizationId]
    LEFT JOIN [dbo].[CollectionGroup] CG ON CG.[CollectionId] = C.[Id] AND CG.[GroupId] = TG.[GroupId]
WHERE CG.[CollectionId] IS NULL;

-- Step 4: Update [dbo].[Group] to clear AccessAll flag
UPDATE G
SET [AccessAll] = 0
FROM [dbo].[Group] G
INNER JOIN #TempGroup TG ON G.[Id] = TG.[GroupId]

-- Step 5: Bump the account revision date for each unique CollectionId and OrganizationId
DECLARE @CollectionId UNIQUEIDENTIFIER
DECLARE @OrganizationId UNIQUEIDENTIFIER

DECLARE CollectionIdCursor CURSOR FOR
SELECT DISTINCT C.[Id], C.[OrganizationId]
FROM [dbo].[Collection] C
INNER JOIN #TempGroup TG ON C.[OrganizationId] = TG.[OrganizationId]

OPEN CollectionIdCursor
FETCH NEXT FROM CollectionIdCursor INTO @CollectionId, @OrganizationId

WHILE (@@FETCH_STATUS = 0)
BEGIN
    -- Execute the stored procedure for the current CollectionId and OrganizationId
    EXEC [dbo].[User_BumpAccountRevisionDateByCollectionId] @CollectionId, @OrganizationId

    -- Fetch the next CollectionId and OrganizationId
    FETCH NEXT FROM CollectionIdCursor INTO @CollectionId, @OrganizationId
END

CLOSE CollectionIdCursor
DEALLOCATE CollectionIdCursor;

-- Step 6: Drop the temporary table
DROP TABLE #TempGroup;
