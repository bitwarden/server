CREATE OR ALTER PROCEDURE [dbo].[Send_ReadByItemIds]
    @ItemIds AS [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        S.*
    FROM
        [dbo].[SendView] S
        INNER JOIN
        @ItemIds I ON S.[Data] LIKE '%' + CONVERT(VARCHAR(36), I.[Id]) + '%'
    WHERE
        S.[Type] = 2
END
GO
