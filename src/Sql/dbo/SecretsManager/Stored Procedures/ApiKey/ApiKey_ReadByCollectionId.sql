CREATE PROCEDURE [dbo].[ApiKey_ReadByCollectionId]
    @CollectionId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[ApiKeyView]
    WHERE
        [CollectionId] = @CollectionId
END
