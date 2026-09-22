IF OBJECT_ID('[dbo].[Collection_ReadOrganizationCollectionsWithPermissions]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Collection_ReadOrganizationCollectionsWithPermissions]
END
GO

CREATE PROCEDURE [dbo].[Collection_ReadOrganizationCollectionsWithPermissions]
    @OrganizationId [UNIQUEIDENTIFIER],
    @UserId [UNIQUEIDENTIFIER]
AS
BEGIN
    SET NOCOUNT ON;

    -- STEP 1. Copy the organization id into a local variable for the two loading queries.
    -- SQL Server compiles a procedure the first time it runs and reuses that plan for every
    -- later caller. Filtering the loading queries on a local variable makes SQL Server plan
    -- for the average organization instead of the first caller's size. Do NOT inline @OrgId
    -- or replace it with OPTIMIZE FOR UNKNOWN.
    DECLARE @OrgId [UNIQUEIDENTIFIER] = @OrganizationId;

    -- STEP 2. Find the caller's membership row in this organization.
    DECLARE @OrganizationUserId [UNIQUEIDENTIFIER] = (
        SELECT TOP 1 [Id]
        FROM [dbo].[OrganizationUser]
        WHERE [OrganizationId] = @OrganizationId
          AND [UserId] = @UserId
    );

    -- STEP 3. Load every direct member grant in the organization into a work table.
    -- Walks collections (not members) to find grants. Measured cheaper for the common
    -- large-org shape (many members, fewer collections). See source doc for numbers.
    SELECT
        [CU].[CollectionId],
        [CU].[OrganizationUserId],
        [CU].[ReadOnly],
        [CU].[HidePasswords],
        [CU].[Manage]
    INTO #OrgCollectionUser
    FROM [dbo].[Collection] [C]
    INNER JOIN [dbo].[CollectionUser] [CU] ON [CU].[CollectionId] = [C].[Id]
    WHERE [C].[OrganizationId] = @OrgId; -- @OrgId, not @OrganizationId: see STEP 1

    -- STEP 4. Load every group grant in the organization into a second work table.
    SELECT
        [CG].[CollectionId],
        [CG].[GroupId],
        [CG].[ReadOnly],
        [CG].[HidePasswords],
        [CG].[Manage]
    INTO #OrgCollectionGroup
    FROM [dbo].[Group] [G]
    INNER JOIN [dbo].[CollectionGroup] [CG] ON [CG].[GroupId] = [G].[Id]
    WHERE [G].[OrganizationId] = @OrgId; -- @OrgId, not @OrganizationId: see STEP 1

    -- STEP 5. Which groups is the caller in?
    DECLARE @UserGroups TABLE ([GroupId] [UNIQUEIDENTIFIER] NOT NULL PRIMARY KEY);
    INSERT INTO @UserGroups ([GroupId])
    SELECT [GroupId]
    FROM [dbo].[GroupUser]
    WHERE [OrganizationUserId] = @OrganizationUserId;

    -- STEP 6. Result set 1: shared collections (Type = 0) with the caller's effective permission.
    SELECT
        [C].*,
        MIN(CASE
            WHEN COALESCE([CU].[ReadOnly], [CG].[ReadOnly], 0) = 0 THEN 0
            ELSE 1
        END) AS [ReadOnly],
        MIN(CASE
            WHEN COALESCE([CU].[HidePasswords], [CG].[HidePasswords], 0) = 0 THEN 0
            ELSE 1
        END) AS [HidePasswords],
        MAX(CASE
            WHEN COALESCE([CU].[Manage], [CG].[Manage], 0) = 0 THEN 0
            ELSE 1
        END) AS [Manage],
        MAX(CASE
            WHEN [CU].[CollectionId] IS NULL AND [CG].[CollectionId] IS NULL THEN 0
            ELSE 1
        END) AS [Assigned],
        CASE
            WHEN NOT EXISTS (SELECT 1 FROM #OrgCollectionUser [X] WHERE [X].[CollectionId] = [C].[Id] AND [X].[Manage] = 1)
                AND NOT EXISTS (SELECT 1 FROM #OrgCollectionGroup [Y] WHERE [Y].[CollectionId] = [C].[Id] AND [Y].[Manage] = 1)
            THEN 1
            ELSE 0
        END AS [Unmanaged],
        MAX(CASE WHEN [AR].[Enabled] = 1 THEN 1 ELSE 0 END) AS [HasEnabledAccessRule]
    FROM [dbo].[CollectionView] [C]
    LEFT JOIN #OrgCollectionUser [CU]
        ON [CU].[CollectionId] = [C].[Id] AND [CU].[OrganizationUserId] = @OrganizationUserId
    LEFT JOIN @UserGroups [UG] ON [CU].[CollectionId] IS NULL
    LEFT JOIN #OrgCollectionGroup [CG]
        ON [CG].[CollectionId] = [C].[Id] AND [CG].[GroupId] = [UG].[GroupId]
    LEFT JOIN [dbo].[AccessRule] [AR] ON [AR].[Id] = [C].[AccessRuleId]
    WHERE [C].[OrganizationId] = @OrganizationId
      AND [C].[Type] = 0
    GROUP BY
        [C].[Id], [C].[OrganizationId], [C].[Name], [C].[CreationDate], [C].[RevisionDate],
        [C].[ExternalId], [C].[DefaultUserCollectionEmail], [C].[Type], [C].[AccessRuleId];

    -- STEP 7. Result set 2: My Items collections (Type = 1) — always returned.
    -- Access Intelligence needs these to attribute items in a member's personal vault
    -- to that member. The existing endpoint omits them; this procedure always includes them.
    -- Same logic as step 6 with Type = 1 and flags cast to BIT.
    SELECT
        [C].*,
        CAST(MIN(CASE
            WHEN COALESCE([CU].[ReadOnly], [CG].[ReadOnly], 0) = 0 THEN 0
            ELSE 1
        END) AS BIT) AS [ReadOnly],
        CAST(MIN(CASE
            WHEN COALESCE([CU].[HidePasswords], [CG].[HidePasswords], 0) = 0 THEN 0
            ELSE 1
        END) AS BIT) AS [HidePasswords],
        CAST(MAX(CASE
            WHEN COALESCE([CU].[Manage], [CG].[Manage], 0) = 0 THEN 0
            ELSE 1
        END) AS BIT) AS [Manage],
        CAST(MAX(CASE
            WHEN [CU].[CollectionId] IS NULL AND [CG].[CollectionId] IS NULL THEN 0
            ELSE 1
        END) AS BIT) AS [Assigned],
        CAST(CASE
            WHEN NOT EXISTS (SELECT 1 FROM #OrgCollectionUser [X] WHERE [X].[CollectionId] = [C].[Id] AND [X].[Manage] = 1)
                AND NOT EXISTS (SELECT 1 FROM #OrgCollectionGroup [Y] WHERE [Y].[CollectionId] = [C].[Id] AND [Y].[Manage] = 1)
            THEN 1
            ELSE 0
        END AS BIT) AS [Unmanaged],
        CAST(MAX(CASE WHEN [AR].[Enabled] = 1 THEN 1 ELSE 0 END) AS BIT) AS [HasEnabledAccessRule]
    FROM [dbo].[CollectionView] [C]
    LEFT JOIN #OrgCollectionUser [CU]
        ON [CU].[CollectionId] = [C].[Id] AND [CU].[OrganizationUserId] = @OrganizationUserId
    LEFT JOIN @UserGroups [UG] ON [CU].[CollectionId] IS NULL
    LEFT JOIN #OrgCollectionGroup [CG]
        ON [CG].[CollectionId] = [C].[Id] AND [CG].[GroupId] = [UG].[GroupId]
    LEFT JOIN [dbo].[AccessRule] [AR] ON [AR].[Id] = [C].[AccessRuleId]
    WHERE [C].[OrganizationId] = @OrganizationId
      AND [C].[Type] = 1
    GROUP BY
        [C].[Id], [C].[OrganizationId], [C].[Name], [C].[CreationDate], [C].[RevisionDate],
        [C].[ExternalId], [C].[DefaultUserCollectionEmail], [C].[Type], [C].[AccessRuleId];

    -- STEP 8. Result sets 3 and 4: who is assigned to each collection.
    -- Group grants first, then member grants. The C# reader depends on that order.
    SELECT [CollectionId], [GroupId], [ReadOnly], [HidePasswords], [Manage]
    FROM #OrgCollectionGroup;

    SELECT [CollectionId], [OrganizationUserId], [ReadOnly], [HidePasswords], [Manage]
    FROM #OrgCollectionUser;
END
GO
