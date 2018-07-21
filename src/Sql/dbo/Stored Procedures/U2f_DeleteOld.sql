CREATE PROCEDURE [dbo].[U2f_DeleteOld]
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @BatchSize INT = 100
    DECLARE @Threshold DATETIME2(7) = DATEADD (day, -7, GETUTCDATE())

    WHILE @BatchSize > 0
    BEGIN
        DELETE TOP(@BatchSize)
        FROM
            [dbo].[U2f]
        WHERE
            [CreationDate] < @Threshold

        SET @BatchSize = @@ROWCOUNT
    END
END