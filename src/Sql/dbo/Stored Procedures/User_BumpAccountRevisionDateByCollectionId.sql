CREATE PROCEDURE [dbo].[User_BumpAccountRevisionDateByCollectionId]
    @CollectionId UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        U
    SET
        U.[AccountRevisionDate] = GETUTCDATE()
    FROM
        [dbo].[User] U
    INNER JOIN
        [dbo].[OrganizationUser] OU ON OU.[UserId] = U.[Id]
    LEFT JOIN
        [dbo].[CollectionUser] CU ON OU.[AccessAll] = 0 AND CU.[OrganizationUserId] = OU.[Id] AND CU.[CollectionId] = @CollectionId
    LEFT JOIN
        [dbo].[GroupUser] GU ON CU.[CollectionId] IS NULL AND OU.[AccessAll] = 0 AND GU.[OrganizationUserId] = OU.[Id]
    LEFT JOIN
        [dbo].[Group] G ON G.[Id] = GU.[GroupId]
    LEFT JOIN
        [dbo].[CollectionGroup] CG ON G.[AccessAll] = 0 AND CG.[GroupId] = GU.[GroupId] AND CG.[CollectionId] = @CollectionId
    WHERE
        OU.[OrganizationId] = @OrganizationId
        AND OU.[Status] = 2 -- 2 = Confirmed
        AND (
            CU.[CollectionId] IS NOT NULL
            OR CG.[CollectionId] IS NOT NULL
            OR OU.[AccessAll] = 1
            OR G.[AccessAll] = 1
        )
END
