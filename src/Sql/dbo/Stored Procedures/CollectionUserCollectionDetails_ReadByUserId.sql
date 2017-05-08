CREATE PROCEDURE [dbo].[CollectionUserCollectionDetails_ReadByUserId]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[CollectionUserCollectionDetailsView]
    WHERE
        [UserId] = @UserId
END