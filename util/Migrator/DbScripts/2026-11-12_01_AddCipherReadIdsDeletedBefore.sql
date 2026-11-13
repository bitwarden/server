CREATE OR ALTER PROCEDURE [dbo].[Cipher_ReadIdsDeletedBefore]
    @DeletedDateBefore DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        [Id]
    FROM
        [dbo].[Cipher]
    WHERE
        [DeletedDate] < @DeletedDateBefore
END
GO
