CREATE PROCEDURE [dbo].[Collection_ReadByIdWithPermissions]
    @CollectionId UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER,
    @IncludeAccessRelationships BIT
AS
BEGIN
    SET NOCOUNT ON

	SELECT
	    C.*,
	    CASE
	        WHEN
	            COALESCE(CU.[ReadOnly], CG.[ReadOnly], 0) = 0
	        THEN 0
	        ELSE 1
	    END [ReadOnly],
	    CASE
	        WHEN
	            COALESCE(CU.[HidePasswords], CG.[HidePasswords], 0) = 0
	        THEN 0
	        ELSE 1
	    END [HidePasswords],
	    CASE
	        WHEN
	            COALESCE(CU.[Manage], CG.[Manage], 0) = 0
	        THEN 0
	        ELSE 1
	    END [Manage],
	    CASE
	    	WHEN CU.[CollectionId] IS NULL AND CG.[CollectionId] IS NULL
	    	THEN 0
	    	ELSE 1
	    END [Assigned]
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
	    C.[Id] = @CollectionId

   IF (@IncludeAccessRelationships = 1)
    BEGIN
        EXEC [dbo].[CollectionGroup_ReadByCollectionId] @CollectionId
        EXEC [dbo].[CollectionUser_ReadByCollectionId] @CollectionId
	END
END
