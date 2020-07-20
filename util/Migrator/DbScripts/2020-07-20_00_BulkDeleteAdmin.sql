IF OBJECT_ID('[dbo].[Cipher_ReadManyById]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Cipher_ReadManyById]
END
GO

CREATE PROCEDURE [dbo].[Cipher_ReadManyById]
    @Ids AS [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[CipherView]
    WHERE
        [Id] IN (SELECT * FROM @Ids)
END
GO
