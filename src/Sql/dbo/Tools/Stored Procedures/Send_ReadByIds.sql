CREATE PROCEDURE [dbo].[Send_ReadByIds]
    @Ids AS [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[SendView]
    WHERE
        [Id] IN (SELECT * FROM @Ids)
END