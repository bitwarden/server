CREATE PROCEDURE [dbo].[CollectionUser_ReadByOrganizationUserIds]
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
    WHERE
        C.[Type] != 1 -- Exclude DefaultUserCollection
END
