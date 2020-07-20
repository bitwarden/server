CREATE PROCEDURE [dbo].[Cipher_ReadManyById]
    @Ids AS [dbo].[GuidIdArray] READONLY,
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[CipherView]
    WHERE
        [Id] = IN (SELECT * FROM @Id)
END
