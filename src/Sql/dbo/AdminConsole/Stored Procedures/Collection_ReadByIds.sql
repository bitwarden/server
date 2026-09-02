CREATE PROCEDURE [dbo].[Collection_ReadByIds]
    @Ids AS [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    IF (SELECT COUNT(1) FROM @Ids) < 1
        BEGIN
            RETURN(-1)
        END

    SELECT
        *
    FROM
        [dbo].[Collection]
    WHERE
        [Id] IN (SELECT [Id] FROM @Ids)
END