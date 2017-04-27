CREATE PROCEDURE [dbo].[CollectionUserCollectionDetails_ReadByUserId]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        CU.*
    FROM
        [dbo].[CollectionUserCollectionDetailsView] CU
    INNER JOIN
        [OrganizationUser] OU ON CU.[OrganizationUserId] = OU.[Id]
    WHERE
        OU.[UserId] = @UserId
END