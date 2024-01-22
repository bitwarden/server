CREATE PROCEDURE [dbo].[Organization_EnableCollectionEnhancements]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    -- Step 1: AccessAll migration for Groups
        -- Create a temporary table to store the groups with AccessAll = 1
        SELECT [Id] AS [GroupId], [OrganizationId]
        INTO #TempGroupsAccessAll
        FROM [dbo].[Group]
        WHERE [OrganizationId] = @OrganizationId
          AND [AccessAll] = 1;

    -- Step 2: AccessAll migration for OrganizationUsers
        -- Create a temporary table to store the OrganizationUsers with AccessAll = 1
        SELECT [Id] AS [OrganizationUserId], [OrganizationId]
        INTO #TempUsersAccessAll
        FROM [dbo].[OrganizationUser]
        WHERE [OrganizationId] = @OrganizationId
          AND [AccessAll] = 1;

    -- Step 3: For all OrganizationUsers with Manager role or 'EditAssignedCollections' permission update their existing CollectionUser rows and insert new rows with [Manage] = 1
    -- and finally update all OrganizationUsers with Manager role to User role
        -- Create a temporary table to store the OrganizationUsers with Manager role or 'EditAssignedCollections' permission
        SELECT ou.[Id] AS [OrganizationUserId],
            CASE WHEN ou.[Type] = 3 THEN 1 ELSE 0 END AS [IsManager]
        INTO #TempUserManagers
        FROM [dbo].[OrganizationUser] ou
        WHERE ou.[OrganizationId] = @OrganizationId
            AND (ou.[Type] = 3 OR (ou.[Permissions] IS NOT NULL
                AND ISJSON(ou.[Permissions]) > 0 AND JSON_VALUE(ou.[Permissions], '$.editAssignedCollections') = 'true'));

    -- Step 4: Bump AccountRevisionDate for all OrganizationUsers updated in the previous steps
        -- Combine and union the distinct OrganizationUserIds from all steps into a single variable
        DECLARE @OrgUsersToBump [dbo].[GuidIdArray]
        INSERT INTO @OrgUsersToBump
        SELECT DISTINCT [OrganizationUserId] AS Id
        FROM (
            -- Step 1
            SELECT GU.[OrganizationUserId]
            FROM [dbo].[GroupUser] GU
            INNER JOIN #TempGroupsAccessAll TG ON GU.[GroupId] = TG.[GroupId]

            UNION

            -- Step 2
            SELECT [OrganizationUserId]
            FROM #TempUsersAccessAll

            UNION

            -- Step 3
            SELECT [OrganizationUserId]
            FROM #TempUserManagers
        ) AS CombinedOrgUsers;

        BEGIN TRY
            BEGIN TRANSACTION;
                -- Step 1
                    -- Update existing rows in [dbo].[CollectionGroup]
                    UPDATE CG
                    SET
                        CG.[ReadOnly] = 0,
                        CG.[HidePasswords] = 0,
                        CG.[Manage] = 0
                    FROM [dbo].[CollectionGroup] CG
                    INNER JOIN [dbo].[Collection] C ON CG.[CollectionId] = C.[Id]
                    INNER JOIN #TempGroupsAccessAll TG ON CG.[GroupId] = TG.[GroupId]
                    WHERE C.[OrganizationId] = TG.[OrganizationId];

                    -- Insert new rows into [dbo].[CollectionGroup]
                    INSERT INTO [dbo].[CollectionGroup] ([CollectionId], [GroupId], [ReadOnly], [HidePasswords], [Manage])
                    SELECT C.[Id], TG.[GroupId], 0, 0, 0
                    FROM [dbo].[Collection] C
                    INNER JOIN #TempGroupsAccessAll TG ON C.[OrganizationId] = TG.[OrganizationId]
                    LEFT JOIN [dbo].[CollectionGroup] CG ON CG.[CollectionId] = C.[Id] AND CG.[GroupId] = TG.[GroupId]
                    WHERE CG.[CollectionId] IS NULL;

                    -- Update Group to clear AccessAll flag
                    UPDATE G
                    SET [AccessAll] = 0
                    FROM [dbo].[Group] G
                    INNER JOIN #TempGroupsAccessAll TG ON G.[Id] = TG.[GroupId];

                -- Step 2
                    -- Update existing rows in [dbo].[CollectionUser]
                    UPDATE target
                    SET
                        target.[ReadOnly] = 0,
                        target.[HidePasswords] = 0,
                        target.[Manage] = 0
                        FROM [dbo].[CollectionUser] AS target
                        INNER JOIN (
                            SELECT C.[Id] AS [CollectionId], TU.[OrganizationUserId]
                            FROM [dbo].[Collection] C
                            INNER JOIN #TempUsersAccessAll TU ON C.[OrganizationId] = TU.[OrganizationId]
                        ) AS source
                    ON target.[CollectionId] = source.[CollectionId] AND target.[OrganizationUserId] = source.[OrganizationUserId];

                    -- Insert new rows into [dbo].[CollectionUser]
                    INSERT INTO [dbo].[CollectionUser] ([CollectionId], [OrganizationUserId], [ReadOnly], [HidePasswords], [Manage])
                    SELECT source.[CollectionId], source.[OrganizationUserId], 0, 0, 0
                    FROM (
                             SELECT C.[Id] AS [CollectionId], TU.[OrganizationUserId]
                             FROM [dbo].[Collection] C
                             INNER JOIN #TempUsersAccessAll TU ON C.[OrganizationId] = TU.[OrganizationId]
                         ) AS source
                    LEFT JOIN [dbo].[CollectionUser] AS target
                        ON target.[CollectionId] = source.[CollectionId] AND target.[OrganizationUserId] = source.[OrganizationUserId]
                    WHERE target.[CollectionId] IS NULL;

                    -- Update OrganizationUser to clear AccessAll flag
                    UPDATE OU
                    SET [AccessAll] = 0
                    FROM [dbo].[OrganizationUser] OU
                    INNER JOIN #TempUsersAccessAll TU ON OU.[Id] = TU.[OrganizationUserId];

                -- Step 3
                    -- Update [dbo].[CollectionUser] with [Manage] = 1 using the temporary table
                    UPDATE CU
                    SET CU.[ReadOnly] = 0,
                        CU.[HidePasswords] = 0,
                        CU.[Manage] = 1
                    FROM [dbo].[CollectionUser] CU
                    INNER JOIN #TempUserManagers TUM ON CU.[OrganizationUserId] = TUM.[OrganizationUserId];

                    -- Insert rows to [dbo].[CollectionUser] with [Manage] = 1 using the temporary table
                    -- This is for orgUsers who are Managers / EditAssignedCollections but have access via a group
                    -- We cannot give the whole group Manage permissions so we have to give them a direct assignment
                    INSERT INTO [dbo].[CollectionUser] ([CollectionId], [OrganizationUserId], [ReadOnly], [HidePasswords], [Manage])
                    SELECT CG.[CollectionId], TUM.[OrganizationUserId], 0, 0, 1
                    FROM [dbo].[CollectionGroup] CG
                    INNER JOIN [dbo].[GroupUser] GU ON CG.[GroupId] = GU.[GroupId]
                    INNER JOIN #TempUserManagers TUM ON GU.[OrganizationUserId] = TUM.[OrganizationUserId]
                    WHERE NOT EXISTS (
                        SELECT 1 FROM [dbo].[CollectionUser] CU
                        WHERE CU.[CollectionId] = CG.[CollectionId] AND CU.[OrganizationUserId] = TUM.[OrganizationUserId]
                    );

                    -- Update [dbo].[OrganizationUser] to migrate all OrganizationUsers with Manager role to User role
                    UPDATE OU
                    SET OU.[Type] = 2 -- User
                    FROM [dbo].[OrganizationUser] OU
                    INNER JOIN #TempUserManagers TUM ON ou.[Id] = TUM.[OrganizationUserId]
                    WHERE TUM.[IsManager] = 1; -- Filter for Managers

                -- Step 4
                    -- Execute User_BumpAccountRevisionDateByOrganizationUserIds for the distinct OrganizationUserIds
                    EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationUserIds] @OrgUsersToBump;
            COMMIT TRANSACTION;
        END TRY
        BEGIN CATCH
            ROLLBACK TRANSACTION;
            THROW;
        END CATCH;

        -- Drop the temporary table
        DROP TABLE #TempGroupsAccessAll;
        DROP TABLE #TempUsersAccessAll;
        DROP TABLE #TempUserManagers;
END
