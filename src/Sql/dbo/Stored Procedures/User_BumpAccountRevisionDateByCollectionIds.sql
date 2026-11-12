CREATE PROCEDURE [dbo].[User_BumpAccountRevisionDateByCollectionIds]
    @CollectionIds AS [dbo].[GuidIdArray] READONLY,
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
    WHERE
        U.[Id] IN (
            SELECT
                OU.[UserId]
            FROM
                @CollectionIds C
            INNER JOIN
                [dbo].[CollectionUser] CU ON CU.[CollectionId] = C.[Id]
            INNER JOIN
                [dbo].[OrganizationUser] OU ON OU.[Id] = CU.[OrganizationUserId]
            WHERE
                OU.[OrganizationId] = @OrganizationId
                AND OU.[Status] = 2 -- Confirmed

            UNION

            SELECT
                OU.[UserId]
            FROM
                @CollectionIds C
            INNER JOIN
                [dbo].[CollectionGroup] CG ON CG.[CollectionId] = C.[Id]
            INNER JOIN
                [dbo].[GroupUser] GU ON GU.[GroupId] = CG.[GroupId]
            INNER JOIN
                [dbo].[OrganizationUser] OU ON OU.[Id] = GU.[OrganizationUserId]
            WHERE
                OU.[OrganizationId] = @OrganizationId
                AND OU.[Status] = 2 -- Confirmed
        )
END
