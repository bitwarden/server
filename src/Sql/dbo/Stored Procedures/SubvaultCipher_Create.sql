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
END