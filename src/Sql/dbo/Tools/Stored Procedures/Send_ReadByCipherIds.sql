CREATE PROCEDURE [dbo].[Send_ReadByCipherIds]
    @CipherIds AS [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[SendView]
    WHERE
        [CipherId] IN (SELECT * FROM @CipherIds)
END
