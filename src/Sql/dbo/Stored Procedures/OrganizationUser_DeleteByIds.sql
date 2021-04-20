CREATE PROCEDURE [dbo].[OrganizationUser_DeleteByIds]
    @Ids [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationUserIds] @Ids

    DECLARE @UserAndOrganizationIds [dbo].[TwoGuidIdArray]

    INSERT INTO @UserAndOrganizationIds
        (Id1, Id2)
    SELECT
        UserId,
        OrganizationId
    FROM
        [dbo].[OrganizationUser] OU
        INNER JOIN
        @Ids OUIds on OUIds.Id = OU.Id
    WHERE
        UserId IS NOT NULL AND
        OrganizationId IS NOT NULL

    BEGIN
        EXEC [dbo].[SsoUser_DeleteMany] @UserAndOrganizationIds
    END

    DELETE CU
    FROM
        [dbo].[CollectionUser] CU
        INNER JOIN
        @Ids I ON I.Id = CU.OrganizationUserId

    DELETE GU
    FROM
        [dbo].[GroupUser] GU
        INNER JOIN
        @IDs I ON I.Id = GU.OrganizationUserId

    DELETE OU
    FROM
        [dbo].[OrganizationUser] OU
        INNER JOIN
        @IDs I ON I.Id = OU.Id
END
GO
