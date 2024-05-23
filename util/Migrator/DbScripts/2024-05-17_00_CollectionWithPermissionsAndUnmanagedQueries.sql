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
                -- No active user or group has manage rights
                NOT EXISTS(
                    SELECT 1
                    FROM [dbo].[CollectionUser] CU2
                             JOIN [dbo].[OrganizationUser] OU2 ON CU2.[OrganizationUserId] = OU2.[Id]
                    WHERE
                        CU2.[CollectionId] = C.[Id] AND
                        OU2.[Status] = 2 AND
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
        C.[ExternalId]

    IF (@IncludeAccessRelationships = 1)
        BEGIN
            EXEC [dbo].[CollectionGroup_ReadByOrganizationId] @OrganizationId
            EXEC [dbo].[CollectionUser_ReadByOrganizationId] @OrganizationId
        END
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
                -- No active user or group has manage rights
                NOT EXISTS(
                    SELECT 1
                    FROM [dbo].[CollectionUser] CU2
                             JOIN [dbo].[OrganizationUser] OU2 ON CU2.[OrganizationUserId] = OU2.[Id]
                    WHERE
                        CU2.[CollectionId] = C.[Id] AND
                        OU2.[Status] = 2 AND
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
        C.[Id] = @CollectionId
    GROUP BY
        C.[Id],
        C.[OrganizationId],
        C.[Name],
        C.[CreationDate],
        C.[RevisionDate],
        C.[ExternalId]

    IF (@IncludeAccessRelationships = 1)
        BEGIN
            EXEC [dbo].[CollectionGroup_ReadByCollectionId] @CollectionId
            EXEC [dbo].[CollectionUser_ReadByCollectionId] @CollectionId
        END
END
GO
