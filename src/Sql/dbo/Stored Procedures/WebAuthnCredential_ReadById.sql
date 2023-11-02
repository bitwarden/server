CREATE PROCEDURE [dbo].[WebAuthnCredential_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[WebAuthnCredentialView]
    WHERE
        [Id] = @Id
END