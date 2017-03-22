CREATE PROCEDURE [dbo].[Cipher_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DELETE
    FROM
        [dbo].[Cipher]
    WHERE
        [Id] = @Id
END
