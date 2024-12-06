CREATE OR ALTER PROCEDURE [dbo].[CipherOrganizationPermissions_GetManyByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        C.[Id],
        C.[OrganizationId],
        MAX(CASE
	        WHEN
	        	CU.[CollectionId] IS NULL AND CG.[CollectionId] IS NULL
	        THEN 0
	        ELSE 1
        END) [Read],
        MAX(CASE
            WHEN COALESCE(CU.[HidePasswords], CG.[HidePasswords], 1) = 0
            THEN 1
            ELSE 0
        END) [ViewPassword],
        MAX(CASE
            WHEN COALESCE(CU.[ReadOnly], CG.[ReadOnly], 1) = 0
            THEN 1
            ELSE 0
        END) [Edit],

        MAX(COALESCE(CU.[Manage], CG.[Manage], 0)) [Manage],
        CASE
            WHEN COUNT(CC.[CollectionId]) > 0 THEN 0
            ELSE 1
        END [Unassigned]
    FROM
        [dbo].[CipherDetails](@UserId) C
    INNER JOIN
        [OrganizationUser] OU ON
        C.[UserId] IS NULL
        AND C.[OrganizationId] = @OrganizationId
        AND OU.[UserId] = @UserId
    INNER JOIN
        [dbo].[Organization] O ON
        O.[Id] = OU.[OrganizationId]
        AND O.[Id] = C.[OrganizationId]
        AND O.[Enabled] = 1
    LEFT JOIN
        [dbo].[CollectionCipher] CC ON
        CC.[CipherId] = C.[Id]
    LEFT JOIN
        [dbo].[CollectionUser] CU ON
        CU.[CollectionId] = CC.[CollectionId]
        AND CU.[OrganizationUserId] = OU.[Id]
    LEFT JOIN
        [dbo].[GroupUser] GU ON
        CU.[CollectionId] IS NULL
        AND GU.[OrganizationUserId] = OU.[Id]
    LEFT JOIN
        [dbo].[Group] G ON
        G.[Id] = GU.[GroupId]
    LEFT JOIN
        [dbo].[CollectionGroup] CG ON
        CG.[CollectionId] = CC.[CollectionId]
        AND CG.[GroupId] = GU.[GroupId]
    GROUP BY
        C.[Id],
       	C.[OrganizationId]
END
GO
