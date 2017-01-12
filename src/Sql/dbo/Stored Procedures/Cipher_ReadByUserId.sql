CREATE PROCEDURE [dbo].[Cipher_ReadByUserId]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[CipherView]
    WHERE
        [UserId] = @UserId
END