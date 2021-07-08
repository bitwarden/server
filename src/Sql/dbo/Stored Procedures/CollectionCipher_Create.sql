CREATE PROCEDURE [dbo].[CollectionCipher_Create]
    @CollectionId UNIQUEIDENTIFIER,
    @CipherId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[CollectionCipher]
    (
        [CollectionId],
        [CipherId]
    )
    VALUES
    (
        @CollectionId,
        @CipherId
    )

    DECLARE @OrganizationId UNIQUEIDENTIFIER = (SELECT TOP 1 [OrganizationId] FROM [dbo].[Cipher] WHERE [Id] = @CipherId)
    IF @OrganizationId IS NOT NULL
    BEGIN
        EXEC [dbo].[User_BumpAccountRevisionDateByCollectionId] @CollectionId, @OrganizationId
    END
END
