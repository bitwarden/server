CREATE PROCEDURE [dbo].[OpaqueKeyExchangeCredential_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[OpaqueKeyExchangeCredential]
    WHERE
        [Id] = @Id
END
