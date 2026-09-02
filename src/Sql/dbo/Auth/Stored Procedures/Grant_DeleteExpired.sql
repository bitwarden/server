CREATE PROCEDURE [dbo].[Grant_DeleteExpired]
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @BatchSize INT = 100
    DECLARE @Now DATETIME2(7) = GETUTCDATE()

    WHILE @BatchSize > 0
    BEGIN
        DELETE TOP(@BatchSize)
        FROM
            [dbo].[Grant]
        WHERE
            [ExpirationDate] < @Now

        SET @BatchSize = @@ROWCOUNT
    END
END