CREATE PROCEDURE [dbo].[CollectionUserDetails_ReadByCollectionId]
    @CollectionId UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        OU.[Id] AS [OrganizationUserId],
        CASE
            WHEN OU.[AccessAll] = 1 OR G.[AccessAll] = 1 THEN 1
            ELSE 0
        END [AccessAll],
        U.[Name],
        ISNULL(U.[Email], OU.[Email]) Email,
        OU.[Status],
        OU.[Type],
        CASE
            WHEN OU.[AccessAll] = 1 OR CU.[ReadOnly] = 0 OR G.[AccessAll] = 1 OR CG.[ReadOnly] = 0 THEN 0
            ELSE 1
        END [ReadOnly]
    FROM
        [dbo].[OrganizationUser] OU
    LEFT JOIN
        [dbo].[User] U ON U.[Id] = OU.[UserId]
    LEFT JOIN
        [dbo].[CollectionUser] CU ON OU.[AccessAll] = 0 AND CU.[OrganizationUserId] = OU.[Id] AND CU.[CollectionId] = @CollectionId
    LEFT JOIN
        [dbo].[GroupUser] GU ON CU.[CollectionId] IS NULL AND OU.[AccessAll] = 0 AND GU.[OrganizationUserId] = OU.[Id]
    LEFT JOIN
        [dbo].[Group] G ON G.[Id] = GU.[GroupId]
    LEFT JOIN
        [dbo].[CollectionGroup] CG ON G.[AccessAll] = 0 AND CG.[GroupId] = GU.[GroupId] AND CG.[CollectionId] = @CollectionId
    WHERE
        CU.[CollectionId] IS NOT NULL
        OR CG.[CollectionId] IS NOT NULL
        OR (
            OU.[OrganizationId] = @OrganizationId
            AND (
                OU.[AccessAll] = 1
                OR G.[AccessAll] = 1
            )
        )
END
