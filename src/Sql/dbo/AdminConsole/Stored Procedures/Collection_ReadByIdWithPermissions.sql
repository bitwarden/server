CREATE PROCEDURE [dbo].[Collection_ReadByIdWithPermissions]
    @CollectionId UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER,
    @IncludeAccessRelationships BIT
AS
BEGIN
    SET NOCOUNT ON

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
