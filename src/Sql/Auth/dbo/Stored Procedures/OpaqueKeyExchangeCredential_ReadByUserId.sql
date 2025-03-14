CREATE PROCEDURE [dbo].[OpaqueKeyExchangeCredential_ReadByUserId]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[OpaqueKeyExchangeCredential]
    WHERE
        [UserId] = @UserId
END
