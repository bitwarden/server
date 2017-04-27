CREATE PROCEDURE [dbo].[CollectionUserCollectionDetails_ReadByUserId]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        SU.*
    FROM
        [dbo].[CollectionUserCollectionDetailsView] SU
    INNER JOIN
        [OrganizationUser] OU ON SU.[OrganizationUserId] = OU.[Id]
    WHERE
        OU.[UserId] = @UserId
END