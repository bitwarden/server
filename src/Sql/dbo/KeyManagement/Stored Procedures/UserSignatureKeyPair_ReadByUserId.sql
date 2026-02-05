CREATE PROCEDURE [dbo].[UserSignatureKeyPair_ReadByUserId]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        *
    FROM
        [dbo].[UserSignatureKeyPairView]
    WHERE
        [UserId] = @UserId;
END
