CREATE PROCEDURE [dbo].[Collection_ReadManagingUserIds]
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
