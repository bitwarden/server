CREATE PROCEDURE [dbo].[Organization_EnableCollectionEnhancements_Part2]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    -- Step 1: For all OrganizationUsers with Manager role or 'EditAssignedCollections' permission update their existing CollectionUser rows and insert new rows with [Manage] = 1
    -- and finally update all OrganizationUsers with Manager role or Custom users with only 'EditAssignedCollections' or 'DeleteAssignedCollections' to User role
        -- Create a temporary table to store the OrganizationUsers with Manager role or with 'EditAssignedCollections' or 'DeleteAssignedCollections' permissions
        SELECT ou.[Id] AS [OrganizationUserId],
            CASE WHEN ou.[Type] = 3 THEN 1 ELSE 0 END AS [IsManager],
            CASE WHEN ou.[Type] = 4 AND
                         ou.[Permissions] IS NOT NULL AND
                         ISJSON(ou.[Permissions]) > 0 AND
                         JSON_VALUE(ou.[Permissions], '$.editAssignedCollections') = 'true'
                    THEN 1 ELSE 0 END AS [EditAssignedCollections],
            CASE WHEN ou.[Type] = 4 AND
                         ou.[Permissions] IS NOT NULL AND
                         ISJSON(ou.[Permissions]) > 0 AND
                         (
                            SELECT COUNT(*)
                            FROM OPENJSON(ou.[Permissions])
                            WHERE [key] NOT IN ('editAssignedCollections', 'deleteAssignedCollections') AND [value] = 'true'
                         ) = 0
                    THEN 1 ELSE 0 END AS [AllOtherPermissionsFalse]
        INTO #TempUserManagers
        FROM [dbo].[OrganizationUser] ou
        WHERE ou.[OrganizationId] = @OrganizationId
              AND (ou.[Type] = 3 OR
                   (ou.[Type] = 4 AND
                    ou.[Permissions] IS NOT NULL AND
                    ISJSON(ou.[Permissions]) > 0 AND
                    (JSON_VALUE(ou.[Permissions], '$.editAssignedCollections') = 'true' OR
                     JSON_VALUE(ou.[Permissions], '$.deleteAssignedCollections') = 'true')
               ));

    -- Step 2: Bump AccountRevisionDate for all OrganizationUsers updated in the previous step
        -- Combine and union the distinct OrganizationUserIds from all steps into a single variable
        DECLARE @OrgUsersToBump [dbo].[GuidIdArray]
        INSERT INTO @OrgUsersToBump
        SELECT DISTINCT [OrganizationUserId] AS Id
        FROM (
            SELECT [OrganizationUserId]
            FROM #TempUserManagers
        ) AS CombinedOrgUsers;

    BEGIN TRY
        BEGIN TRANSACTION;
            -- Step 1
                -- Update [dbo].[CollectionUser] with [Manage] = 1 using the temporary table
                UPDATE CU
                SET CU.[ReadOnly] = 0,
                    CU.[HidePasswords] = 0,
                    CU.[Manage] = 1
                FROM [dbo].[CollectionUser] CU
                INNER JOIN #TempUserManagers TUM ON CU.[OrganizationUserId] = TUM.[OrganizationUserId]
                WHERE TUM.[IsManager] = 1 OR TUM.[EditAssignedCollections] = 1;

                -- Insert rows to [dbo].[CollectionUser] with [Manage] = 1 using the temporary table
                -- This is for orgUsers who are Managers / EditAssignedCollections but have access via a group
                -- We cannot give the whole group Manage permissions so we have to give them a direct assignment
                INSERT INTO [dbo].[CollectionUser] ([CollectionId], [OrganizationUserId], [ReadOnly], [HidePasswords], [Manage])
                SELECT DISTINCT CG.[CollectionId], TUM.[OrganizationUserId], 0, 0, 1
                FROM [dbo].[CollectionGroup] CG
                INNER JOIN [dbo].[GroupUser] GU ON CG.[GroupId] = GU.[GroupId]
                INNER JOIN #TempUserManagers TUM ON GU.[OrganizationUserId] = TUM.[OrganizationUserId]
                WHERE (TUM.[IsManager] = 1 OR TUM.[EditAssignedCollections] = 1) AND NOT EXISTS (
                    SELECT 1 FROM [dbo].[CollectionUser] CU
                    WHERE CU.[CollectionId] = CG.[CollectionId] AND CU.[OrganizationUserId] = TUM.[OrganizationUserId]
                );

                -- Update [dbo].[OrganizationUser] to migrate all OrganizationUsers with Manager role
                -- or Custom role with only 'DeleteAssignedCollections' or 'EditAssignedCollections' to User role
                UPDATE OU
                SET OU.[Type] = 2, OU.[RevisionDate] = GETUTCDATE() -- User
                FROM [dbo].[OrganizationUser] OU
                INNER JOIN #TempUserManagers TUM ON ou.[Id] = TUM.[OrganizationUserId]
                WHERE TUM.[IsManager] = 1 OR TUM.[AllOtherPermissionsFalse] = 1; -- Filter for Managers and Custom users with no other permissions

            -- Step 2
                -- Execute User_BumpAccountRevisionDateByOrganizationUserIds for the distinct OrganizationUserIds
                EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationUserIds] @OrgUsersToBump;
        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        ROLLBACK TRANSACTION;
        THROW;
    END CATCH;

    -- Drop the temporary table
    DROP TABLE #TempUserManagers;
END
