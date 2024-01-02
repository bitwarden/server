-- TODO: rollback support?

CREATE PROCEDURE [dbo].[Organization_EnableCollectionEnhancements]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    -- AccessAll migration for Groups
    BEGIN TRANSACTION;

        -- Create a temporary table to store the groups with AccessAll = 1
        SELECT [Id] AS [GroupId], [OrganizationId]
        INTO #TempGroup
        FROM [dbo].[Group]
        WHERE [AccessAll] = 1
            AND [OrganizationId] = @Id;

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

        -- Drop the temporary table
        DROP TABLE #TempGroup;

    COMMIT TRANSACTION;

    -- AccessAll migration for users
    BEGIN TRANSACTION;

        -- Step 1: Insert into a temporary table with an additional column for batch processing, update 50 k at a time
        SELECT [Id] AS [OrganizationUserId], [OrganizationId], CAST(ROW_NUMBER() OVER(ORDER BY [Id]) / 50000 AS INT) AS Batch
        INTO #TempOrgUser
        FROM [dbo].[OrganizationUser]
        WHERE [AccessAll] = 1
            AND [OrganizationId] = @Id;

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

        -- Step 4: Drop the temporary table
        DROP TABLE #TempOrgUser;

    COMMIT TRANSACTION;

    BEGIN TRANSACTION;
        -- Update [dbo].[CollectionUser] with [Manage] = 1 for all users with Manager role or 'EditAssignedCollections' permission
        UPDATE cu
        SET cu.[ReadOnly] = 0,
            cu.[HidePasswords] = 0,
            cu.[Manage] = 1
        FROM [dbo].[CollectionUser] cu
        INNER JOIN [dbo].[OrganizationUser] ou
            ON cu.[OrganizationUserId] = ou.[Id]
        WHERE ou.[OrganizationId] = @Id
            AND (ou.[Type] = 3
                OR (ou.[Permissions] IS NOT NULL AND ISJSON(ou.[Permissions]) > 0 AND JSON_VALUE(ou.[Permissions], '$.editAssignedCollections') = 'true'))

        -- Insert rows to [dbo].[CollectionUser] for Managers and users with 'EditAssignedCollections' permission assigned to groups with collection access
        INSERT INTO [dbo].[CollectionUser] ([CollectionId], [OrganizationUserId], [ReadOnly], [HidePasswords], [Manage])
        SELECT cg.[CollectionId], ou.[Id], 0, 0, 1
        FROM [dbo].[CollectionGroup] cg
        INNER JOIN [dbo].[GroupUser] gu
            ON cg.GroupId = gu.GroupId
        INNER JOIN [dbo].[OrganizationUser] ou
            ON gu.OrganizationUserId = ou.[Id]
        WHERE ou.[OrganizationId] = @Id
            AND (ou.[Type] = 3
                OR (ou.[Permissions] IS NOT NULL AND ISJSON(ou.[Permissions]) > 0 AND JSON_VALUE(ou.[Permissions], '$.editAssignedCollections') = 'true'))
            AND NOT EXISTS (
                SELECT 1 FROM [dbo].[CollectionUser] cu
                WHERE cu.[CollectionId] = cg.[CollectionId] AND cu.[OrganizationUserId] = ou.[Id]
            )

    COMMIT TRANSACTION;
END
