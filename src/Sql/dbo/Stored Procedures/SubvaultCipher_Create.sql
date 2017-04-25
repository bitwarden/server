CREATE PROCEDURE [dbo].[SubvaultCipher_Create]
    @SubvaultId UNIQUEIDENTIFIER,
    @CipherId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[SubvaultCipher]
    (
        [SubvaultId],
        [CipherId]
    )
    VALUES
    (
        @SubvaultId,
        @CipherId
    )

    DECLARE @OrganizationId UNIQUEIDENTIFIER = (SELECT TOP 1 [OrganizationId] FROM [dbo].[Cipher] WHERE [Id] = @CipherId)
    IF @OrganizationId IS NOT NULL
    BEGIN
        EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationId] @OrganizationId
    END
END