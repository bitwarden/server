CREATE PROCEDURE [dbo].[Organization_EnableCollectionEnhancements]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    -- Step 1: AccessAll migration for Groups
        -- Create a temporary table to store the groups with AccessAll = 1
        SELECT [Id] AS [GroupId], [OrganizationId]
        INTO #TempGroup
        FROM [dbo].[Group]
        WHERE [AccessAll] = 1
          AND [OrganizationId] = @OrganizationId;

        -- Create a temporary table to store distinct OrganizationUserIds
        SELECT DISTINCT GU.[OrganizationUserId]
        INTO #TempOrganizationUsers
        FROM [dbo].[GroupUser] GU
        JOIN #TempGroup TG ON GU.[GroupId] = TG.[GroupId];

        BEGIN TRY
            BEGIN TRANSACTION;
                -- Update existing rows in [dbo].[CollectionGroup]
                UPDATE CG
                SET
                    CG.[ReadOnly] = 0,
                    CG.[HidePasswords] = 0,
                    CG.[Manage] = 0
                FROM [dbo].[CollectionGroup] CG
                INNER JOIN [dbo].[Collection] C ON CG.[CollectionId] = C.[Id]
                INNER JOIN #TempGroup TG ON CG.[GroupId] = TG.[GroupId]
                WHERE C.[OrganizationId] = TG.[OrganizationId];

                -- Insert new rows into [dbo].[CollectionGroup]
                INSERT INTO [dbo].[CollectionGroup] ([CollectionId], [GroupId], [ReadOnly], [HidePasswords], [Manage])
                SELECT C.[Id], TG.[GroupId], 0, 0, 0
                FROM [dbo].[Collection] C
                INNER JOIN #TempGroup TG ON C.[OrganizationId] = TG.[OrganizationId]
                LEFT JOIN [dbo].[CollectionGroup] CG ON CG.[CollectionId] = C.[Id] AND CG.[GroupId] = TG.[GroupId]
                WHERE CG.[CollectionId] IS NULL;

                -- Update Group to clear AccessAll flag
                UPDATE G
                SET [AccessAll] = 0
                FROM [dbo].[Group] G
                INNER JOIN #TempGroup TG ON G.[Id] = TG.[GroupId]

                -- Execute User_BumpAccountRevisionDateByOrganizationUserId for each unique OrganizationUserId
                DECLARE @Step1OrganizationUserId UNIQUEIDENTIFIER

                DECLARE UniqueOrgUserIdCursor CURSOR FOR
                SELECT [OrganizationUserId]
                FROM #TempOrganizationUsers

                OPEN UniqueOrgUserIdCursor
                FETCH NEXT FROM UniqueOrgUserIdCursor INTO @Step1OrganizationUserId

                WHILE (@@FETCH_STATUS = 0)
                BEGIN
                    -- Execute the stored procedure for the current OrganizationUserId
                    EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationUserId] @Step1OrganizationUserId

                    -- Fetch the next row
                    FETCH NEXT FROM UniqueOrgUserIdCursor INTO @Step1OrganizationUserId
                END

                CLOSE UniqueOrgUserIdCursor
                DEALLOCATE UniqueOrgUserIdCursor;
            COMMIT TRANSACTION;
        END TRY
        BEGIN CATCH
            ROLLBACK TRANSACTION;
            THROW;
        END CATCH;

        -- Drop the temporary table
        DROP TABLE #TempGroup;
        DROP TABLE #TempOrganizationUsers;

    -- Step 2: AccessAll migration for users
        -- Create a temporary table to store the OrganizationUsers with AccessAll = 1
        SELECT [Id] AS [OrganizationUserId], [OrganizationId]
        INTO #TempOrgUser
        FROM [dbo].[OrganizationUser]
        WHERE [AccessAll] = 1
          AND [OrganizationId] = @OrganizationId;

        BEGIN TRY
            BEGIN TRANSACTION;
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
                        INNER JOIN #TempOrgUser T ON C.[OrganizationId] = T.[OrganizationId]
                    ) AS source
                ON target.[CollectionId] = source.[CollectionId] AND target.[OrganizationUserId] = source.[OrganizationUserId];

                -- Insert new rows into [dbo].[CollectionUser]
                INSERT INTO [dbo].[CollectionUser] ([CollectionId], [OrganizationUserId], [ReadOnly], [HidePasswords], [Manage])
                SELECT source.[CollectionId], source.[OrganizationUserId], 0, 0, 0
                FROM (
                         SELECT C.[Id] AS [CollectionId], T.[OrganizationUserId]
                         FROM [dbo].[Collection] C
                             INNER JOIN #TempOrgUser T ON C.[OrganizationId] = T.[OrganizationId]
                     ) AS source
                LEFT JOIN [dbo].[CollectionUser] AS target
                    ON target.[CollectionId] = source.[CollectionId] AND target.[OrganizationUserId] = source.[OrganizationUserId]
                WHERE target.[CollectionId] IS NULL;

                -- Update OrganizationUser to clear AccessAll flag
                UPDATE OU
                SET [AccessAll] = 0
                FROM [dbo].[OrganizationUser] OU
                INNER JOIN #TempOrgUser T ON OU.[Id] = T.[OrganizationUserId]

                -- Execute User_BumpAccountRevisionDateByOrganizationUserId for each unique OrganizationUserId
                DECLARE @Step2OrganizationUserId UNIQUEIDENTIFIER

                DECLARE UniqueOrgUserIdCursor CURSOR FOR
                SELECT DISTINCT [OrganizationUserId]
                FROM #TempOrgUser

                OPEN UniqueOrgUserIdCursor
                FETCH NEXT FROM UniqueOrgUserIdCursor INTO @Step2OrganizationUserId

                WHILE (@@FETCH_STATUS = 0)
                BEGIN
                    -- Execute the stored procedure for the current OrganizationUserId
                    EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationUserId] @Step2OrganizationUserId

                    -- Fetch the next row
                    FETCH NEXT FROM UniqueOrgUserIdCursor INTO @Step2OrganizationUserId
                END

                CLOSE UniqueOrgUserIdCursor
                DEALLOCATE UniqueOrgUserIdCursor;
            COMMIT TRANSACTION;
        END TRY
        BEGIN CATCH
            ROLLBACK TRANSACTION;
            THROW;
        END CATCH;

        -- Drop the temporary table
        DROP TABLE #TempOrgUser;

    -- Step 3: For all OrganizationUsers with Manager role or 'EditAssignedCollections' permission update their existing CollectionUser rows and insert new rows with [Manage] = 1
    -- and finally update all OrganizationUsers with Manager role to User role
        -- Create a temporary table to store the OrganizationUsers with Manager role or 'EditAssignedCollections' permission
        SELECT ou.[Id] AS [OrganizationUserId],
            CASE WHEN ou.[Type] = 3 THEN 1 ELSE 0 END AS [IsManager]
        INTO #TempStep3
        FROM [dbo].[OrganizationUser] ou
        WHERE ou.[OrganizationId] = @OrganizationId AND (ou.[Type] = 3 OR (ou.[Permissions] IS NOT NULL AND
            ISJSON(ou.[Permissions]) > 0 AND JSON_VALUE(ou.[Permissions], '$.editAssignedCollections') = 'true'));

        BEGIN TRY
            BEGIN TRANSACTION;
                -- Update [dbo].[CollectionUser] with [Manage] = 1 using the temporary table
                UPDATE cu
                SET cu.[ReadOnly] = 0,
                    cu.[HidePasswords] = 0,
                    cu.[Manage] = 1
                FROM [dbo].[CollectionUser] cu
                INNER JOIN #TempStep3 temp ON cu.[OrganizationUserId] = temp.[OrganizationUserId];

                -- Insert rows to [dbo].[CollectionUser] with [Manage] = 1 using the temporary table
                INSERT INTO [dbo].[CollectionUser] ([CollectionId], [OrganizationUserId], [ReadOnly], [HidePasswords], [Manage])
                SELECT cg.[CollectionId], ou.[OrganizationUserId], 0, 0, 1
                FROM [dbo].[CollectionGroup] cg
                INNER JOIN [dbo].[GroupUser] gu ON cg.[GroupId] = gu.[GroupId]
                INNER JOIN #TempStep3 ou ON gu.[OrganizationUserId] = ou.[OrganizationUserId]
                WHERE NOT EXISTS (
                    SELECT 1 FROM [dbo].[CollectionUser] cu
                    WHERE cu.[CollectionId] = cg.[CollectionId] AND cu.[OrganizationUserId] = ou.[OrganizationUserId]
                );

                -- Update [dbo].[OrganizationUser] to migrate all OrganizationUsers with Manager role to User role
                UPDATE ou
                SET ou.[Type] = 2 -- User
                FROM [dbo].[OrganizationUser] ou
                INNER JOIN #TempStep3 temp ON ou.[Id] = temp.[OrganizationUserId]
                WHERE temp.[IsManager] = 1; -- Filter for Managers

                -- Execute User_BumpAccountRevisionDateByOrganizationUserId for each unique OrganizationUserId
                DECLARE @Step3OrganizationUserId UNIQUEIDENTIFIER

                DECLARE UniqueOrgUserIdCursor CURSOR FOR
                SELECT [OrganizationUserId]
                FROM #TempStep3

                OPEN UniqueOrgUserIdCursor
                FETCH NEXT FROM UniqueOrgUserIdCursor INTO @Step3OrganizationUserId

                WHILE (@@FETCH_STATUS = 0)
                BEGIN
                    -- Execute the stored procedure for the current OrganizationUserId
                    EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationUserId] @Step3OrganizationUserId

                    -- Fetch the next row
                    FETCH NEXT FROM UniqueOrgUserIdCursor INTO @Step3OrganizationUserId
                END

                CLOSE UniqueOrgUserIdCursor
                DEALLOCATE UniqueOrgUserIdCursor;
            COMMIT TRANSACTION;
        END TRY
        BEGIN CATCH
            ROLLBACK TRANSACTION;
            THROW;
        END CATCH;

        -- Drop the temporary table
        DROP TABLE #TempStep3;
END
GO
