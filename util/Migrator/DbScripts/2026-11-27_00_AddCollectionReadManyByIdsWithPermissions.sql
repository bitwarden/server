-- Add Collection_ReadManyByIdsWithPermissions: batched version of Collection_ReadByIdWithPermissions.
-- Unlike the single-collection version, it only counts a Confirmed member of an Enabled org as Assigned or Manage.

CREATE OR ALTER PROCEDURE [dbo].[Collection_ReadManyByIdsWithPermissions]
    @OrganizationId UNIQUEIDENTIFIER,
    @Ids [dbo].[GuidIdArray] READONLY,
    @UserId UNIQUEIDENTIFIER,
    @IncludeAccessRelationships BIT
AS
BEGIN
    SET NOCOUNT ON

    -- The caller's membership in this org. It only counts if it is Confirmed and the org is Enabled.
    DECLARE @Memberships TABLE (
        [OrganizationUserId] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        [OrganizationId] UNIQUEIDENTIFIER NOT NULL
    )

    INSERT INTO @Memberships ([OrganizationUserId], [OrganizationId])
    SELECT
        OU.[Id],
        OU.[OrganizationId]
    FROM
        [dbo].[OrganizationUser] OU
    INNER JOIN
        [dbo].[Organization] O ON O.[Id] = OU.[OrganizationId]
    WHERE
        OU.[UserId] = @UserId
        AND OU.[OrganizationId] = @OrganizationId
        AND OU.[Status] = 2 -- Confirmed
        AND O.[Enabled] = 1

    -- The groups the caller is in, through those memberships.
    DECLARE @UserGroups TABLE (
        [OrganizationUserId] UNIQUEIDENTIFIER NOT NULL,
        [GroupId] UNIQUEIDENTIFIER NOT NULL,
        PRIMARY KEY ([OrganizationUserId], [GroupId])
    )

    INSERT INTO @UserGroups ([OrganizationUserId], [GroupId])
    SELECT
        GU.[OrganizationUserId],
        GU.[GroupId]
    FROM
        [dbo].[GroupUser] GU
    INNER JOIN
        @Memberships M ON M.[OrganizationUserId] = GU.[OrganizationUserId]

    -- A #temp table (not the TVP) so the statements below are planned for the number of ids actually passed.
    -- The unnamed inline primary key lets SQL Server cache the table between calls.
    -- Only ids in @OrganizationId go in, so every result set below is scoped to that org.
    CREATE TABLE #CollectionIds (
        [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY
    )

    INSERT INTO #CollectionIds ([Id])
    SELECT DISTINCT
        I.[Id]
    FROM
        @Ids I
    INNER JOIN
        [dbo].[Collection] C ON C.[Id] = I.[Id]
    WHERE
        C.[OrganizationId] = @OrganizationId

    SELECT
        C.*,
        -- ReadOnly and HidePasswords are the Edit (2) and ViewPassword (1) parts of the highest rank, inverted; no rank means 3
        1 - (ISNULL(MAX(R.[Rank]), 3) / 2) % 2 AS [ReadOnly],
        1 - ISNULL(MAX(R.[Rank]), 3) % 2 AS [HidePasswords],
        MAX(CASE
            WHEN
                COALESCE(CU.[Manage], CG.[Manage], 0) = 0
            THEN 0
            ELSE 1
        END) AS [Manage],
        MAX(CASE
            WHEN
                CU.[CollectionId] IS NULL AND CG.[CollectionId] IS NULL
            THEN 0
            ELSE 1
        END) AS [Assigned],
        CASE
            WHEN
                -- No user or group has manage rights
                NOT EXISTS (
                    SELECT 1
                    FROM [dbo].[CollectionUser] CU2
                    WHERE
                        CU2.[CollectionId] = C.[Id] AND
                        CU2.[Manage] = 1
                )
                AND NOT EXISTS (
                    SELECT 1
                    FROM [dbo].[CollectionGroup] CG2
                    WHERE
                        CG2.[CollectionId] = C.[Id] AND
                        CG2.[Manage] = 1
                )
            THEN 1
            ELSE 0
        END AS [Unmanaged],
        MAX(CASE WHEN AR.[Enabled] = 1 THEN 1 ELSE 0 END) AS [HasEnabledAccessRule]
    FROM
        [dbo].[CollectionView] C
    INNER JOIN
        #CollectionIds I ON I.[Id] = C.[Id]
    LEFT JOIN
        -- Only a Confirmed member of an Enabled org counts as Assigned or Manage.
        @Memberships M ON M.[OrganizationId] = C.[OrganizationId]
    LEFT JOIN
        [dbo].[CollectionUser] CU ON CU.[CollectionId] = C.[Id] AND CU.[OrganizationUserId] = M.[OrganizationUserId]
    LEFT JOIN
        @UserGroups UG ON CU.[CollectionId] IS NULL AND UG.[OrganizationUserId] = M.[OrganizationUserId]
    LEFT JOIN
        [dbo].[CollectionGroup] CG ON CG.[CollectionId] = C.[Id] AND CG.[GroupId] = UG.[GroupId]
    LEFT JOIN
        [dbo].[AccessRule] AR ON AR.[Id] = C.[AccessRuleId]
    -- Rank orders a grant by Manage, then Edit, then ViewPassword; a group with no grant on this collection has no rank
    CROSS APPLY (
        SELECT
            CASE
                WHEN CU.[CollectionId] IS NULL AND CG.[CollectionId] IS NULL THEN NULL
                ELSE
                    CASE WHEN COALESCE(CU.[Manage], CG.[Manage], 0) = 1 THEN 4 ELSE 0 END
                    + CASE WHEN COALESCE(CU.[ReadOnly], CG.[ReadOnly], 0) = 0 THEN 2 ELSE 0 END
                    + CASE WHEN COALESCE(CU.[HidePasswords], CG.[HidePasswords], 0) = 0 THEN 1 ELSE 0 END
            END AS [Rank]
    ) R
    GROUP BY
        C.[Id],
        C.[OrganizationId],
        C.[Name],
        C.[CreationDate],
        C.[RevisionDate],
        C.[ExternalId],
        C.[DefaultUserCollectionEmail],
        C.[Type],
        C.[AccessRuleId]

    IF (@IncludeAccessRelationships = 1)
    BEGIN
        -- Return CollectionId so the caller knows which collection each row belongs to.
        SELECT
            CG.[CollectionId],
            CG.[GroupId],
            CG.[ReadOnly],
            CG.[HidePasswords],
            CG.[Manage]
        FROM
            [dbo].[CollectionGroup] CG
        INNER JOIN
            #CollectionIds I ON I.[Id] = CG.[CollectionId]

        SELECT
            CU.[CollectionId],
            CU.[OrganizationUserId],
            CU.[ReadOnly],
            CU.[HidePasswords],
            CU.[Manage]
        FROM
            [dbo].[CollectionUser] CU
        INNER JOIN
            #CollectionIds I ON I.[Id] = CU.[CollectionId]
    END
END
GO
