CREATE PROCEDURE [dbo].[WebAuthnCredential_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DELETE
    FROM
        [dbo].[WebAuthnCredential]
    WHERE
        [Id] = @Id
END