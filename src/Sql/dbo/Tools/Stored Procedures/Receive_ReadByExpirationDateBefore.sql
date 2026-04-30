CREATE PROCEDURE [dbo].[Receive_ReadByExpirationDateBefore]
    @ExpirationDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[ReceiveView]
    WHERE
        [ExpirationDate] < @ExpirationDate
END
