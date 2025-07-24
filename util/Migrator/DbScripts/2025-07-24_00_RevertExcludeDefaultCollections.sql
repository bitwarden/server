CREATE OR ALTER PROCEDURE [dbo].[Collection_ReadByOrganizationIdWithPermissions]
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
	    END AS [Unmanaged]
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
	WHERE
	    C.[OrganizationId] = @OrganizationId
    GROUP BY
    	C.[Id],
    	C.[OrganizationId],
    	C.[Name],
    	C.[CreationDate],
    	C.[RevisionDate],
    	C.[ExternalId],
    	C.[DefaultUserCollectionEmail],
    	C.[Type]

   IF (@IncludeAccessRelationships = 1)
    BEGIN
        EXEC [dbo].[CollectionGroup_ReadByOrganizationId] @OrganizationId
        EXEC [dbo].[CollectionUser_ReadByOrganizationId] @OrganizationId
    END
END
GO

CREATE OR ALTER PROCEDURE [dbo].[Collection_ReadByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[CollectionView]
    WHERE
        [OrganizationId] = @OrganizationId
END
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationUserUserDetails_ReadWithCollectionsById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    EXEC [OrganizationUserUserDetails_ReadById] @Id

    SELECT
        CU.[CollectionId] Id,
        CU.[ReadOnly],
        CU.[HidePasswords],
        CU.[Manage]
    FROM
        [dbo].[OrganizationUser] OU
    INNER JOIN
        [dbo].[CollectionUser] CU ON CU.[OrganizationUserId] = [OU].[Id]
    INNER JOIN
        [dbo].[Collection] C ON CU.[CollectionId] = C.[Id]
    WHERE
        [OrganizationUserId] = @Id
END
GO

CREATE OR ALTER PROCEDURE [dbo].[CollectionUser_ReadByOrganizationUserIds]
    @OrganizationUserIds [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        CU.*
    FROM
        [dbo].[OrganizationUser] OU
    INNER JOIN
        [dbo].[CollectionUser] CU ON CU.[OrganizationUserId] = OU.[Id]
    INNER JOIN
        [dbo].[Collection] C ON CU.[CollectionId] = C.[Id]
    INNER JOIN
        @OrganizationUserIds OUI ON OUI.[Id] = OU.[Id]
END
GO
