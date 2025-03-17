CREATE PROCEDURE [dbo].[OpaqueKeyExchangeCredential_DeleteById]
    @Id UNIQUEIDENTIFIER,
AS
BEGIN
    DELETE
    FROM
        [dbo].[OpaqueKeyExchangeCredential]
    WHERE
        [Id] = @Id
END