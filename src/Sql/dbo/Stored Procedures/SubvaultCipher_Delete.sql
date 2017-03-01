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
END