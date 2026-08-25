-- Add Collection_ReadManagingUserIds: the distinct confirmed members who can Manage a collection. PAM's approver
-- inbox notifier fans a RefreshApproverInbox push out to exactly this set.

CREATE OR ALTER PROCEDURE [dbo].[Collection_ReadManagingUserIds]
    @CollectionId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    -- Every confirmed member who can Manage the collection: direct Manage assignments, Manage via group membership,
    -- plus org Owners/Admins (when the org allows admin access to all collection items) and Custom users with the
    -- EditAnyCollection permission. Returns distinct user ids.
    DECLARE @OrganizationId UNIQUEIDENTIFIER
    SELECT @OrganizationId = [OrganizationId] FROM [dbo].[Collection] WHERE [Id] = @CollectionId

    SELECT DISTINCT [UserId]
    FROM
    (
        SELECT OU.[UserId]
        FROM [dbo].[CollectionUser] CU
        INNER JOIN [dbo].[OrganizationUser] OU ON OU.[Id] = CU.[OrganizationUserId]
        WHERE CU.[CollectionId] = @CollectionId
            AND CU.[Manage] = 1
            AND OU.[Status] = 2 -- Confirmed
            AND OU.[UserId] IS NOT NULL

        UNION

        SELECT OU.[UserId]
        FROM [dbo].[CollectionGroup] CG
        INNER JOIN [dbo].[GroupUser] GU ON GU.[GroupId] = CG.[GroupId]
        INNER JOIN [dbo].[OrganizationUser] OU ON OU.[Id] = GU.[OrganizationUserId]
        WHERE CG.[CollectionId] = @CollectionId
            AND CG.[Manage] = 1
            AND OU.[Status] = 2 -- Confirmed
            AND OU.[UserId] IS NOT NULL

        UNION

        SELECT OU.[UserId]
        FROM [dbo].[OrganizationUser] OU
        INNER JOIN [dbo].[Organization] O ON O.[Id] = OU.[OrganizationId]
        WHERE OU.[OrganizationId] = @OrganizationId
            AND OU.[Status] = 2 -- Confirmed
            AND OU.[UserId] IS NOT NULL
            AND (
                (O.[AllowAdminAccessToAllCollectionItems] = 1 AND OU.[Type] IN (0, 1)) -- Owner, Admin
                OR (OU.[Type] = 4 -- Custom
                    AND ISJSON(OU.[Permissions]) = 1
                    AND JSON_VALUE(OU.[Permissions], '$.editAnyCollection') = 'true')
            )
    ) AS ManagingUsers
END
GO

-- Surface [HasEnabledAccessRule] on the collection read paths that feed sync, the org collection
-- listing and the single-collection read. The Collection.AccessRuleId association was already
-- selected by these procedures, but "governed" only means "gated" while the rule itself is
-- switched on, so each now LEFT JOINs [dbo].[AccessRule] and reports whether the governing rule
-- is enabled rather than merely present.

-- Collection_ReadByUserId
CREATE OR ALTER PROCEDURE [dbo].[Collection_ReadByUserId]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        UCD.[Id],
        UCD.[OrganizationId],
        UCD.[Name],
        UCD.[CreationDate],
        UCD.[RevisionDate],
        UCD.[ExternalId],
        MIN(UCD.[ReadOnly]) AS [ReadOnly],
        MIN(UCD.[HidePasswords]) AS [HidePasswords],
        MAX(UCD.[Manage]) AS [Manage],
        UCD.[DefaultUserCollectionEmail],
        UCD.[Type],
        UCD.[AccessRuleId],
        MAX(CASE WHEN AR.[Enabled] = 1 THEN 1 ELSE 0 END) AS [HasEnabledAccessRule]
    FROM
        [dbo].[UserCollectionDetails](@UserId) UCD
    LEFT JOIN
        [dbo].[AccessRule] AR ON AR.[Id] = UCD.[AccessRuleId]
    GROUP BY
        UCD.[Id],
        UCD.[OrganizationId],
        UCD.[Name],
        UCD.[CreationDate],
        UCD.[RevisionDate],
        UCD.[ExternalId],
        UCD.[DefaultUserCollectionEmail],
        UCD.[Type],
        UCD.[AccessRuleId]
END
GO

-- Collection_ReadByIdWithPermissions
CREATE OR ALTER PROCEDURE [dbo].[Collection_ReadByIdWithPermissions]
    @CollectionId UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER,
    @IncludeAccessRelationships BIT
AS
BEGIN
    SET NOCOUNT ON

	SELECT
	    C.*,
	    MIN(CASE
	        WHEN
	            COALESCE(CU.[ReadOnly], CG.[ReadOnly], 0) = 0
	        THEN 0
	        ELSE 1
	    END) AS [ReadOnly],
	    MIN (CASE
	        WHEN
	            COALESCE(CU.[HidePasswords], CG.[HidePasswords], 0) = 0
	        THEN 0
	        ELSE 1
	    END) AS [HidePasswords],
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
                 NOT EXISTS(
                    SELECT 1
                    FROM [dbo].[CollectionUser] CU2
                             JOIN [dbo].[OrganizationUser] OU2 ON CU2.[OrganizationUserId] = OU2.[Id]
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
	LEFT JOIN
	    [dbo].[OrganizationUser] OU ON C.[OrganizationId] = OU.[OrganizationId] AND OU.[UserId] = @UserId
	LEFT JOIN
	    [dbo].[CollectionUser] CU ON CU.[CollectionId] = C.[Id] AND CU.[OrganizationUserId] = [OU].[Id]
	LEFT JOIN
	    [dbo].[GroupUser] GU ON CU.[CollectionId] IS NULL AND GU.[OrganizationUserId] = OU.[Id]
	LEFT JOIN
	    [dbo].[Group] G ON G.[Id] = GU.[GroupId]
	LEFT JOIN
	    [dbo].[CollectionGroup] CG ON CG.[CollectionId] = C.[Id] AND CG.[GroupId] = GU.[GroupId]
	LEFT JOIN
	    [dbo].[AccessRule] AR ON AR.[Id] = C.[AccessRuleId]
	WHERE
	    C.[Id] = @CollectionId
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
        EXEC [dbo].[CollectionGroup_ReadByCollectionId] @CollectionId
        EXEC [dbo].[CollectionUser_ReadByCollectionId] @CollectionId
	END
END
GO

-- Collection_ReadSharedCollectionsByOrganizationIdWithPermissions
CREATE OR ALTER PROCEDURE [dbo].[Collection_ReadSharedCollectionsByOrganizationIdWithPermissions]
    @OrganizationId UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER,
    @IncludeAccessRelationships BIT
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        C.*,
        MIN(CASE
            WHEN
                COALESCE(CU.[ReadOnly], CG.[ReadOnly], 0) = 0
            THEN 0
            ELSE 1
        END) AS [ReadOnly],
        MIN(CASE
            WHEN
                COALESCE(CU.[HidePasswords], CG.[HidePasswords], 0) = 0
            THEN 0
            ELSE 1
        END) AS [HidePasswords],
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
                NOT EXISTS(
                    SELECT 1
                    FROM [dbo].[CollectionUser] CU2
                    JOIN [dbo].[OrganizationUser] OU2 ON CU2.[OrganizationUserId] = OU2.[Id]
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
    LEFT JOIN
        [dbo].[OrganizationUser] OU ON C.[OrganizationId] = OU.[OrganizationId] AND OU.[UserId] = @UserId
    LEFT JOIN
        [dbo].[CollectionUser] CU ON CU.[CollectionId] = C.[Id] AND CU.[OrganizationUserId] = [OU].[Id]
    LEFT JOIN
        [dbo].[GroupUser] GU ON CU.[CollectionId] IS NULL AND GU.[OrganizationUserId] = OU.[Id]
    LEFT JOIN
        [dbo].[Group] G ON G.[Id] = GU.[GroupId]
    LEFT JOIN
        [dbo].[CollectionGroup] CG ON CG.[CollectionId] = C.[Id] AND CG.[GroupId] = GU.[GroupId]
    LEFT JOIN
        [dbo].[AccessRule] AR ON AR.[Id] = C.[AccessRuleId]
    WHERE
        C.[OrganizationId] = @OrganizationId AND
        C.[Type] = 0 -- Only SharedCollection
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
        EXEC [dbo].[CollectionGroup_ReadByOrganizationId] @OrganizationId
        EXEC [dbo].[CollectionUser_ReadByOrganizationId] @OrganizationId
    END
END
GO
