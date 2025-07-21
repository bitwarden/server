CREATE PROCEDURE [dbo].[CollectionCipher_RemoveCollectionsForManyCiphers]
    @CipherIds AS [dbo].[GuidIdArray] READONLY,
    @OrganizationId UNIQUEIDENTIFIER,
    @CollectionIds AS [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @BatchSize INT = 100

    WHILE @BatchSize > 0
    BEGIN
        BEGIN TRANSACTION CollectionCipher_DeleteMany
            DELETE TOP(@BatchSize)
            FROM
                [dbo].[CollectionCipher]
            WHERE
                [CipherId] IN (SELECT [Id] FROM @CipherIds) AND
                [CollectionId] IN (SELECT [Id] FROM @CollectionIds)

            SET @BatchSize = @@ROWCOUNT
       COMMIT TRANSACTION CollectionCipher_DeleteMany
    END

    EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationId] @OrganizationId
END
