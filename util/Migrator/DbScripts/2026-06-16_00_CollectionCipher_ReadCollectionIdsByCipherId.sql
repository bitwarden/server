CREATE OR ALTER PROCEDURE [dbo].[CollectionCipher_ReadCollectionIdsByCipherId]
    @CipherId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        [CollectionId]
    FROM
        [dbo].[CollectionCipher]
    WHERE
        [CipherId] = @CipherId
END
