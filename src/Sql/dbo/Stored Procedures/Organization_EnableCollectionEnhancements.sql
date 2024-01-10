CREATE PROCEDURE [dbo].[Organization_EnableCollectionEnhancements]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    BEGIN TRY
        BEGIN TRANSACTION;
        -- Step 1: AccessAll migration for Groups
            -- Create a temporary table to store the groups with AccessAll = 1
            SELECT [Id] AS [GroupId], [OrganizationId]
            INTO #TempGroup
            FROM [dbo].[Group]
            WHERE [AccessAll] = 1
              AND [OrganizationId] = @OrganizationId;

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

            -- Drop the temporary table
            DROP TABLE #TempGroup;

        -- Step 2: AccessAll migration for users
            -- Step 1: Create a temporary table to store the OrganizationUsers with AccessAll = 1
            SELECT [Id] AS [OrganizationUserId], [OrganizationId]
            INTO #TempOrgUser
            FROM [dbo].[OrganizationUser]
            WHERE [AccessAll] = 1
              AND [OrganizationId] = @OrganizationId;

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

            -- Drop the temporary table
            DROP TABLE #TempOrgUser;

        -- Step 3: Update [dbo].[CollectionUser] with [Manage] = 1 for all users with Manager role or 'EditAssignedCollections' permission
            UPDATE cu
            SET cu.[ReadOnly] = 0,
                cu.[HidePasswords] = 0,
                cu.[Manage] = 1
            FROM [dbo].[CollectionUser] cu
            INNER JOIN [dbo].[OrganizationUser] ou
                ON cu.[OrganizationUserId] = ou.[Id]
            WHERE ou.[OrganizationId] = @OrganizationId
              AND (ou.[Type] = 3
               OR (ou.[Permissions] IS NOT NULL AND ISJSON(ou.[Permissions]) > 0 AND JSON_VALUE(ou.[Permissions], '$.editAssignedCollections') = 'true'))

        -- Step 4: Insert rows to [dbo].[CollectionUser] for Managers and users with 'EditAssignedCollections' permission assigned to groups with collection access
            INSERT INTO [dbo].[CollectionUser] ([CollectionId], [OrganizationUserId], [ReadOnly], [HidePasswords], [Manage])
            SELECT cg.[CollectionId], ou.[Id], 0, 0, 1
            FROM [dbo].[CollectionGroup] cg
            INNER JOIN [dbo].[GroupUser] gu
                ON cg.GroupId = gu.GroupId
            INNER JOIN [dbo].[OrganizationUser] ou
                ON gu.OrganizationUserId = ou.[Id]
            WHERE ou.[OrganizationId] = @OrganizationId
                AND (ou.[Type] = 3
                    OR (ou.[Permissions] IS NOT NULL AND ISJSON(ou.[Permissions]) > 0 AND JSON_VALUE(ou.[Permissions], '$.editAssignedCollections') = 'true'))
                AND NOT EXISTS (
                    SELECT 1 FROM [dbo].[CollectionUser] cu
                    WHERE cu.[CollectionId] = cg.[CollectionId] AND cu.[OrganizationUserId] = ou.[Id]
                )

        -- Step 5: Set all Managers to Users
            UPDATE [dbo].[OrganizationUser]
            SET [Type] = 2  -- User
            WHERE [OrganizationId] = @OrganizationId
              AND [Type] = 3; -- Manager

        -- Step 6: Bump the account revision dates for all users in the organization
        EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationId] @OrganizationId

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END
GO
