CREATE PROCEDURE [dbo].[CollectionCipher_ReadUserIdsByCollectionIds]
    @CollectionIds AS [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    -- Users with direct collection access
    SELECT DISTINCT OU.[UserId]
    FROM [dbo].[CollectionUser] CU
    INNER JOIN [dbo].[OrganizationUser] OU ON OU.[Id] = CU.[OrganizationUserId]
    WHERE CU.[CollectionId] IN (SELECT [Id] FROM @CollectionIds)
        AND OU.[Status] = 2 -- Confirmed

    UNION

    -- Users with group-based collection access
    SELECT DISTINCT OU.[UserId]
    FROM [dbo].[CollectionGroup] CG
    INNER JOIN [dbo].[GroupUser] GU ON GU.[GroupId] = CG.[GroupId]
    INNER JOIN [dbo].[OrganizationUser] OU ON OU.[Id] = GU.[OrganizationUserId]
    WHERE CG.[CollectionId] IN (SELECT [Id] FROM @CollectionIds)
        AND OU.[Status] = 2 -- Confirmed
END
