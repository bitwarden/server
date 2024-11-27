CREATE PROCEDURE [dbo].[SecurityTask_ReadByUserIdStatus]
    @UserId UNIQUEIDENTIFIER,
    @Status TINYINT = NULL
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        ST.Id,
        ST.OrganizationId,
        ST.CipherId,
        ST.Type,
        ST.Status,
        ST.CreationDate,
        ST.RevisionDate
    FROM
        [dbo].[SecurityTaskView] ST
    INNER JOIN
        [dbo].[OrganizationUserView] OU ON OU.[OrganizationId] = ST.[OrganizationId]
    INNER JOIN
        [dbo].[Organization] O ON O.[Id] = ST.[OrganizationId]
    LEFT JOIN
        [dbo].[CipherView] C ON C.[Id] = ST.[CipherId]
    LEFT JOIN
        [dbo].[CollectionCipher] CC ON CC.[CipherId] = C.[Id] AND C.[Id] IS NOT NULL
    LEFT JOIN
        [dbo].[CollectionUser] CU ON CU.[CollectionId] = CC.[CollectionId] AND CU.[OrganizationUserId] = OU.[Id] AND C.[Id] IS NOT NULL
    LEFT JOIN
        [dbo].[GroupUser] GU ON GU.[OrganizationUserId] = OU.[Id] AND CU.[CollectionId] IS NULL AND C.[Id] IS NOT NULL
    LEFT JOIN
        [dbo].[CollectionGroup] CG ON CG.[GroupId] = GU.[GroupId] AND CG.[CollectionId] = CC.[CollectionId]
    WHERE
        OU.[UserId] = @UserId
        AND OU.[Status] = 2 -- Ensure user is confirmed
        AND O.[Enabled] = 1
        AND (
            ST.[CipherId] IS NULL
            OR (
                C.[Id] IS NOT NULL
                AND (
                    CU.[ReadOnly] = 0
                    OR CG.[Manage] = 0
                )
            )
        )
        AND ST.[Status] = COALESCE(@Status, ST.[Status])
    GROUP BY
        ST.Id,
        ST.OrganizationId,
        ST.CipherId,
        ST.Type,
        ST.Status,
        ST.CreationDate,
        ST.RevisionDate
    ORDER BY ST.[CreationDate] DESC
END
