CREATE PROCEDURE [dbo].[AccessLease_ReadManyActiveByRequesterId]
    @RequesterId UNIQUEIDENTIFIER,
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[AccessLease]
    WHERE
        [RequesterId] = @RequesterId
        AND [Action] = 0 -- None (no early end)
        AND [NotBefore] <= @Now
        AND [NotAfter] > @Now
    ORDER BY
        [NotAfter] ASC
END
