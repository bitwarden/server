CREATE PROCEDURE [dbo].[Cipher_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SELECT
        *
    FROM
        [dbo].[CipherView]
    WHERE
        [Id] = @Id
END
