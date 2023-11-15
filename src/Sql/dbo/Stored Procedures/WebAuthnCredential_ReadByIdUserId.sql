CREATE PROCEDURE [dbo].[WebAuthnCredential_ReadByIdUserId]
    @Id UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[WebAuthnCredentialView]
    WHERE
        [Id] = @Id
    AND
        [UserId] = @UserId
END
