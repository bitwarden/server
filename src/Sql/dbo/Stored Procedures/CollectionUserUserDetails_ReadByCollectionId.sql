CREATE PROCEDURE [dbo].[CollectionUserUserDetails_ReadByCollectionId]
    @CollectionId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[CollectionUserUserDetailsView]
    WHERE
        [AccessAll] = 1 
        OR [CollectionId] = @CollectionId
END