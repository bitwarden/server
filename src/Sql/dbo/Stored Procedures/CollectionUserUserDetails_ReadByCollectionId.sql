CREATE PROCEDURE [dbo].[CollectionUserUserDetails_ReadByCollectionId]
    @CollectionId UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[CollectionUserUserDetailsView]
    WHERE
        [CollectionId] = @CollectionId
        OR
        (
            [OrganizationId] = @OrganizationId
            AND [AccessAll] = 1
        )
END
