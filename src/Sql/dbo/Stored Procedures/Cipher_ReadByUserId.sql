CREATE PROCEDURE [dbo].[Cipher_ReadByUserId]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SELECT
        *
    FROM
        [dbo].[CipherView]
    WHERE
        [UserId] = @UserId
END
