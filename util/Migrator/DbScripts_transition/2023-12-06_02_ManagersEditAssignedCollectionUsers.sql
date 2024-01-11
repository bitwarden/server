-- Step 1: Update [dbo].[CollectionUser] with [Manage] = 1 for all users with Manager role or 'EditAssignedCollections' permission
    -- Store the results in a temporary table
    SELECT ou.[Id] AS [OrganizationUserId]
    INTO #TempStep1
    FROM [dbo].[OrganizationUser] ou
    WHERE (ou.[Type] = 3 OR (ou.[Permissions] IS NOT NULL AND
        ISJSON(ou.[Permissions]) > 0 AND JSON_VALUE(ou.[Permissions], '$.editAssignedCollections') = 'true'));

    -- Update [dbo].[CollectionUser] with [Manage] = 1 using the temporary table
    UPDATE cu
    SET cu.[ReadOnly] = 0,
        cu.[HidePasswords] = 0,
        cu.[Manage] = 1
    FROM [dbo].[CollectionUser] cu
    INNER JOIN #TempStep1 temp ON cu.[OrganizationUserId] = temp.[OrganizationUserId];

-- Step 2: Insert rows to [dbo].[CollectionUser] for Managers and users with 'EditAssignedCollections' permission assigned to groups with collection access
    -- Store the results in a temporary table
    SELECT cg.[CollectionId], ou.[Id] AS [OrganizationUserId]
    INTO #TempStep2
    FROM [dbo].[CollectionGroup] cg
    INNER JOIN [dbo].[GroupUser] gu ON cg.GroupId = gu.GroupId
    INNER JOIN [dbo].[OrganizationUser] ou ON gu.OrganizationUserId = ou.[Id]
    WHERE (ou.[Type] = 3 OR (ou.[Permissions] IS NOT NULL AND ISJSON(ou.[Permissions]) > 0 AND JSON_VALUE(ou.[Permissions], '$.editAssignedCollections') = 'true'))
      AND NOT EXISTS (
        SELECT 1 FROM [dbo].[CollectionUser] cu
        WHERE cu.[CollectionId] = cg.[CollectionId] AND cu.[OrganizationUserId] = ou.[Id]
        );

    -- Insert rows into [dbo].[CollectionUser] using the temporary table
    INSERT INTO [dbo].[CollectionUser] ([CollectionId], [OrganizationUserId], [ReadOnly], [HidePasswords], [Manage])
    SELECT [CollectionId], [OrganizationUserId], 0, 0, 1
    FROM #TempStep2;

-- Step 3: Set all Managers to Users
    -- Store the results in a temporary table
    SELECT [Id] AS [OrganizationUserId]
    INTO #TempStep3
    FROM [dbo].[OrganizationUser]
    WHERE [Type] = 3; -- Manager

    -- Update [dbo].[OrganizationUser] based on the temporary table
    UPDATE ou
    SET ou.[Type] = 2 -- User
    FROM [dbo].[OrganizationUser] ou
    INNER JOIN #TempStep3 temp ON ou.[Id] = temp.[OrganizationUserId];

-- Step 4: Bump the account revision date for each unique OrganizationUserId in #TempStep1, #TempStep2, and #TempStep3
    -- Join the three temporary tables to get unique OrganizationUserId
    SELECT DISTINCT temp1.[OrganizationUserId]
    INTO #TempUniqueOrganizationUser
    FROM #TempStep1 temp1
    JOIN #TempStep2 temp2 ON temp1.[OrganizationUserId] = temp2.[OrganizationUserId]
    JOIN #TempStep3 temp3 ON temp1.[OrganizationUserId] = temp3.[OrganizationUserId];

    -- Execute User_BumpAccountRevisionDateByOrganizationUserId for each unique OrganizationUserId
    DECLARE @OrganizationUserId UNIQUEIDENTIFIER

    DECLARE UniqueOrgUserIdCursor CURSOR FOR
    SELECT [OrganizationUserId]
    FROM #TempUniqueOrganizationUser

    OPEN UniqueOrgUserIdCursor
    FETCH NEXT FROM UniqueOrgUserIdCursor INTO @OrganizationUserId

    WHILE (@@FETCH_STATUS = 0)
    BEGIN
        -- Execute the stored procedure for the current OrganizationUserId
        EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationUserId] @OrganizationUserId

        -- Fetch the next row
        FETCH NEXT FROM UniqueOrgUserIdCursor INTO @OrganizationUserId
    END

    CLOSE UniqueOrgUserIdCursor
    DEALLOCATE UniqueOrgUserIdCursor;

-- Step 5: Clean up temporary tables
DROP TABLE #TempStep1;
DROP TABLE #TempStep2;
DROP TABLE #TempStep3;
DROP TABLE #TempUniqueOrganizationUser;
