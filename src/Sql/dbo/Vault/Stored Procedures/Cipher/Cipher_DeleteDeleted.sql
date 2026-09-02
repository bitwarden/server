CREATE PROCEDURE [dbo].[Cipher_DeleteDeleted]
    @DeletedDateBefore DATETIME2 (7)
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @BatchSize INT = 100

    WHILE @BatchSize > 0
    BEGIN
        DELETE TOP(@BatchSize)
        FROM
            [dbo].[Cipher]
        WHERE
            [DeletedDate] < @DeletedDateBefore

        SET @BatchSize = @@ROWCOUNT
    END
END