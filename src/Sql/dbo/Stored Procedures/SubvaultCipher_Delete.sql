CREATE PROCEDURE [dbo].[SubvaultCipher_Delete]
    @SubvaultId UNIQUEIDENTIFIER,
    @CipherId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DELETE
    FROM
        [dbo].[SubvaultCipher]
    WHERE
        [SubvaultId] = @SubvaultId
        AND [CipherId] = @CipherId

    DECLARE @OrganizationId UNIQUEIDENTIFIER = (SELECT TOP 1 [OrganizationId] FROM [dbo].[Cipher] WHERE [Id] = @CipherId)
    IF @OrganizationId IS NOT NULL
    BEGIN
        EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationId] @OrganizationId
    END
END