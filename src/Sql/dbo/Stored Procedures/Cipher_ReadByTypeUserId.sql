CREATE PROCEDURE [dbo].[Cipher_ReadByTypeUserId]
    @Type TINYINT,
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[CipherView]
    WHERE
        [Type] = @Type
        AND [UserId] = @UserId
END