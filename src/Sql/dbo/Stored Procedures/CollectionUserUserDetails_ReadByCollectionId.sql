CREATE PROCEDURE [dbo].[CollectionUserUserDetails_ReadByCollectionId]
    @CollectionId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @OrganizationId UNIQUEIDENTIFIER = (SELECT [OrganizationId] FROM [dbo].[Collection] WHERE [Id] = @CollectionId)

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
