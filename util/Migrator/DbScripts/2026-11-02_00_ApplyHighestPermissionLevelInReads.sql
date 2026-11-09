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
        -- ReadOnly and HidePasswords are the Edit (2) and ViewPassword (1) parts of the highest rank, inverted
        1 - (MAX(R.[Rank]) / 2) % 2 AS [ReadOnly],
        1 - MAX(R.[Rank]) % 2 AS [HidePasswords],
        MAX(UCD.[Manage]) AS [Manage],
        UCD.[DefaultUserCollectionEmail],
        UCD.[Type],
        UCD.[AccessRuleId],
        MAX(CASE WHEN AR.[Enabled] = 1 THEN 1 ELSE 0 END) AS [HasEnabledAccessRule]
    FROM
        [dbo].[UserCollectionDetails](@UserId) UCD
    LEFT JOIN
        [dbo].[AccessRule] AR ON AR.[Id] = UCD.[AccessRuleId]
    -- Rank orders a grant by Manage, then Edit, then ViewPassword, so the highest permission level wins
    CROSS APPLY (
        SELECT UCD.[Manage] * 4 + (1 - UCD.[ReadOnly]) * 2 + (1 - UCD.[HidePasswords]) AS [Rank]
    ) R
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

CREATE OR ALTER PROCEDURE [dbo].[Collection_ReadByIdWithPermissions]
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
GO

CREATE OR ALTER PROCEDURE [dbo].[Collection_ReadOrganizationCollectionsWithPermissions]
    @OrganizationId [UNIQUEIDENTIFIER],
    @UserId [UNIQUEIDENTIFIER]
AS
BEGIN
    SET NOCOUNT ON;

    -- STEP 1. Copy the organization id into a local variable so SQL Server plans for the average organization. Do NOT inline @OrgId.
    DECLARE @OrgId [UNIQUEIDENTIFIER] = @OrganizationId;

    -- STEP 2. Find the caller's membership row in this organization.
    DECLARE @OrganizationUserId [UNIQUEIDENTIFIER] = (
        SELECT TOP 1
            [Id]
        FROM
            [dbo].[OrganizationUser]
        WHERE
            [OrganizationId] = @OrganizationId
            AND [UserId] = @UserId
    );

    -- STEP 3. Load every direct member grant in the organization into a work table.
    SELECT
        [CU].[CollectionId],
        [CU].[OrganizationUserId],
        [CU].[ReadOnly],
        [CU].[HidePasswords],
        [CU].[Manage]
    INTO
        #OrgCollectionUser
    FROM
        [dbo].[Collection] [C]
    INNER JOIN
        [dbo].[CollectionUser] [CU] ON [CU].[CollectionId] = [C].[Id]
    WHERE
        [C].[OrganizationId] = @OrgId; -- @OrgId, not @OrganizationId: see STEP 1

    -- STEP 4. Load every group grant in the organization into a second work table.
    SELECT
        [CG].[CollectionId],
        [CG].[GroupId],
        [CG].[ReadOnly],
        [CG].[HidePasswords],
        [CG].[Manage]
    INTO
        #OrgCollectionGroup
    FROM
        [dbo].[Group] [G]
    INNER JOIN
        [dbo].[CollectionGroup] [CG] ON [CG].[GroupId] = [G].[Id]
    WHERE
        [G].[OrganizationId] = @OrgId; -- @OrgId, not @OrganizationId: see STEP 1

    -- STEP 5. Which groups is the caller in?
    DECLARE @UserGroups TABLE ([GroupId] [UNIQUEIDENTIFIER] NOT NULL PRIMARY KEY);
    INSERT INTO @UserGroups ([GroupId])
    SELECT
        [GroupId]
    FROM
        [dbo].[GroupUser]
    WHERE
        [OrganizationUserId] = @OrganizationUserId;

    -- STEP 6. Result set 1: shared and My Items collections (Type IN (0, 1)) with the caller's effective permission.
    SELECT
        [C].*,
        -- ReadOnly and HidePasswords are the Edit (2) and ViewPassword (1) parts of the highest rank, inverted; no rank means 3
        CAST(1 - (ISNULL(MAX([R].[Rank]), 3) / 2) % 2 AS BIT) AS [ReadOnly],
        CAST(1 - ISNULL(MAX([R].[Rank]), 3) % 2 AS BIT) AS [HidePasswords],
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
    FROM
        [dbo].[CollectionView] [C]
    LEFT JOIN
        #OrgCollectionUser [CU] ON [CU].[CollectionId] = [C].[Id]
        AND [CU].[OrganizationUserId] = @OrganizationUserId
    LEFT JOIN
        @UserGroups [UG] ON [CU].[CollectionId] IS NULL
    LEFT JOIN
        #OrgCollectionGroup [CG] ON [CG].[CollectionId] = [C].[Id]
        AND [CG].[GroupId] = [UG].[GroupId]
    LEFT JOIN
        [dbo].[AccessRule] [AR] ON [AR].[Id] = [C].[AccessRuleId]
    -- Rank orders a grant by Manage, then Edit, then ViewPassword; a group with no grant on this collection has no rank
    CROSS APPLY (
        SELECT
            CASE
                WHEN [CU].[CollectionId] IS NULL AND [CG].[CollectionId] IS NULL THEN NULL
                ELSE
                    CASE WHEN COALESCE([CU].[Manage], [CG].[Manage], 0) = 1 THEN 4 ELSE 0 END
                    + CASE WHEN COALESCE([CU].[ReadOnly], [CG].[ReadOnly], 0) = 0 THEN 2 ELSE 0 END
                    + CASE WHEN COALESCE([CU].[HidePasswords], [CG].[HidePasswords], 0) = 0 THEN 1 ELSE 0 END
            END AS [Rank]
    ) [R]
    WHERE
        [C].[OrganizationId] = @OrganizationId
        AND [C].[Type] IN (0, 1)
    GROUP BY
        [C].[Id],
        [C].[OrganizationId],
        [C].[Name],
        [C].[CreationDate],
        [C].[RevisionDate],
        [C].[ExternalId],
        [C].[DefaultUserCollectionEmail],
        [C].[Type],
        [C].[AccessRuleId];

    -- STEP 7. Result sets 2 and 3: group grants, then member grants. The C# reader depends on that order.
    SELECT
        [CollectionId],
        [GroupId],
        [ReadOnly],
        [HidePasswords],
        [Manage]
    FROM
        #OrgCollectionGroup;

    SELECT
        [CollectionId],
        [OrganizationUserId],
        [ReadOnly],
        [HidePasswords],
        [Manage]
    FROM
        #OrgCollectionUser;
END
GO

CREATE OR ALTER PROCEDURE [dbo].[Collection_ReadSharedCollectionsByOrganizationIdWithPermissions]
    @OrganizationId UNIQUEIDENTIFIER,
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

CREATE OR ALTER PROCEDURE [dbo].[CipherDetails_ReadByIdUserId]
    @Id UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

SELECT
        [Id],
        [UserId],
        [OrganizationId],
        [Type],
        [Data],
        [Attachments],
        [CreationDate],
        [RevisionDate],
        [Favorite],
        [FolderId],
        [DeletedDate],
        [Reprompt],
        [Key],
        [OrganizationUseTotp],
        [ArchivedDate],
        -- The highest rank holds Manage as 4, Edit as 2 and ViewPassword as 1; each flag is one part of it
        (MAX(R.[Rank]) / 2) % 2 AS [Edit],
        MAX(R.[Rank]) % 2 AS [ViewPassword],
        MAX(R.[Rank]) / 4 AS [Manage]
    FROM
        [dbo].[UserCipherDetails](@UserId) UCD
    -- Rank orders a grant by Manage, then Edit, then ViewPassword, so the highest permission level wins
    CROSS APPLY (
        SELECT UCD.[Manage] * 4 + UCD.[Edit] * 2 + UCD.[ViewPassword] AS [Rank]
    ) R
    WHERE
        [Id] = @Id
    GROUP BY
        [Id],
        [UserId],
        [OrganizationId],
        [Type],
        [Data],
        [Attachments],
        [CreationDate],
        [RevisionDate],
        [Favorite],
        [FolderId],
        [DeletedDate],
        [Reprompt],
        [Key],
        [OrganizationUseTotp],
        [ArchivedDate]
END
GO

CREATE OR ALTER PROCEDURE [dbo].[CipherOrganizationPermissions_GetManyByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    ;WITH BaseCiphers AS (
        SELECT C.[Id], C.[OrganizationId]
        FROM [dbo].[CipherDetails](@UserId) C
        INNER JOIN [OrganizationUser] OU ON
            C.[UserId] IS NULL
            AND C.[OrganizationId] = @OrganizationId
            AND OU.[UserId] = @UserId
        INNER JOIN [dbo].[Organization] O ON
            O.[Id] = OU.[OrganizationId]
            AND O.[Id] = C.[OrganizationId]
            AND O.[Enabled] = 1
    ),
    CollectionAccess AS (
        SELECT
            CU.[CollectionId],
            CU.[ReadOnly],
            CU.[HidePasswords],
            CU.[Manage]
        FROM [dbo].[CollectionUser] CU
        WHERE CU.[OrganizationUserId] = (
            SELECT [Id] FROM [OrganizationUser]
            WHERE [UserId] = @UserId
            AND [OrganizationId] = @OrganizationId
        )

        UNION ALL

        -- A direct grant on a collection takes precedence over the user's group grants on it
        SELECT
            CG.[CollectionId],
            CG.[ReadOnly],
            CG.[HidePasswords],
            CG.[Manage]
        FROM [dbo].[CollectionGroup] CG
        INNER JOIN [dbo].[GroupUser] GU ON
            GU.[GroupId] = CG.[GroupId]
            AND GU.[OrganizationUserId] = (
                SELECT [Id] FROM [OrganizationUser]
                WHERE [UserId] = @UserId
                AND [OrganizationId] = @OrganizationId
            )
        WHERE NOT EXISTS (
            SELECT 1
            FROM [dbo].[CollectionUser] CU
            WHERE CU.[CollectionId] = CG.[CollectionId]
            AND CU.[OrganizationUserId] = GU.[OrganizationUserId]
        )
    ),
    CollectionPermissions AS (
        -- Rank orders a grant by Manage, then Edit, then ViewPassword, so the highest permission level wins
        SELECT
            [CollectionId],
            MAX(
                CASE WHEN [Manage] = 1 THEN 4 ELSE 0 END
                + CASE WHEN [ReadOnly] = 0 THEN 2 ELSE 0 END
                + CASE WHEN [HidePasswords] = 0 THEN 1 ELSE 0 END
            ) [Rank]
        FROM CollectionAccess
        GROUP BY [CollectionId]
    ),
    CipherPermissions AS (
        SELECT
            CC.[CipherId],
            MAX(CP.[Rank]) [Rank]
        FROM CollectionPermissions CP
        INNER JOIN [dbo].[CollectionCipher] CC ON CC.[CollectionId] = CP.[CollectionId]
        GROUP BY CC.[CipherId]
    )
    SELECT
        C.[Id],
        C.[OrganizationId],
        -- Read is any rank; ViewPassword (1), Edit (2) and Manage (4) are the parts of the highest rank
        CASE WHEN P.[Rank] IS NULL THEN 0 ELSE 1 END as [Read],
        ISNULL(P.[Rank] % 2, 0) as [ViewPassword],
        ISNULL((P.[Rank] / 2) % 2, 0) as [Edit],
        ISNULL(P.[Rank] / 4, 0) as [Manage]
    FROM BaseCiphers C
    LEFT JOIN CipherPermissions P ON P.CipherId = C.[Id]
END
GO
