-- Step 1: Retrieve OrganizationUsers with [AccessAll] permission
SELECT [Id] AS [OrganizationUserId], [OrganizationId]
INTO #TempOrgUser
FROM [dbo].[OrganizationUser]
WHERE [AccessAll] = 1;

-- Step 2: Declare variables for OrganizationUserId and OrganizationId
DECLARE @OrgUserId UNIQUEIDENTIFIER;
DECLARE @OrganizationId UNIQUEIDENTIFIER;

-- Step 3: Create a cursor to iterate through the results of the temporary table
DECLARE OrgUserCursor CURSOR FOR
SELECT [OrganizationUserId], [OrganizationId]
FROM #TempOrgUser;

OPEN OrgUserCursor;

-- Step 4: Loop through the organization users
FETCH NEXT FROM OrgUserCursor INTO @OrgUserId, @OrganizationId;

WHILE @@FETCH_STATUS = 0
BEGIN
    -- Step 5: Use MERGE to insert or update into [dbo].[CollectionUser] for each [dbo].[Collection] entry
MERGE INTO [dbo].[CollectionUser] AS target
    USING (SELECT C.[Id] AS [CollectionId], @OrgUserId AS [OrganizationUserId] FROM [dbo].[Collection] C WHERE C.[OrganizationId] = @OrganizationId) AS source
    ON (target.[CollectionId] = source.[CollectionId] AND target.[OrganizationUserId] = source.[OrganizationUserId])
    WHEN MATCHED THEN
UPDATE SET
    target.[ReadOnly] = 0,
    target.[HidePasswords] = 0,
    target.[Manage] = 1
    WHEN NOT MATCHED THEN
INSERT ([CollectionId], [OrganizationUserId], [ReadOnly], [HidePasswords], [Manage])
VALUES (source.[CollectionId], source.[OrganizationUserId], 0, 0, 1);

-- Step 6: Fetch the next OrganizationUserId and OrganizationId
FETCH NEXT FROM OrgUserCursor INTO @OrgUserId, @OrganizationId;
END;

-- Step 7: Close and deallocate the cursor
CLOSE OrgUserCursor;
DEALLOCATE OrgUserCursor;

-- Step 8: Drop the temporary table
DROP TABLE #TempOrgUser;
