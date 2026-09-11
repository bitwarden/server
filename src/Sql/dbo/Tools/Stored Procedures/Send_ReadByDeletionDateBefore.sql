CREATE PROCEDURE [dbo].[Send_ReadByDeletionDateBefore]
    @DeletionDate DATETIME2(7),
    @BatchSize INT = 2000
AS
BEGIN
    SET NOCOUNT ON

    SELECT TOP (@BatchSize)
        *
    FROM
        [dbo].[SendView]
    WHERE
        [DeletionDate] < @DeletionDate
    ORDER BY
        [DeletionDate] ASC
END