CREATE PROCEDURE [dbo].[WebAuthnCredential_ReadByUserId]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[WebAuthnCredentialView]
    WHERE
        [UserId] = @UserId
END