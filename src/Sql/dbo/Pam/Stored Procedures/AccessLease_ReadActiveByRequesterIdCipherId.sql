CREATE PROCEDURE [dbo].[AccessLease_ReadActiveByRequesterIdCipherId]
    @RequesterId UNIQUEIDENTIFIER,
    @CipherId UNIQUEIDENTIFIER,
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    SELECT TOP 1
        *
    FROM
        [dbo].[AccessLease]
    WHERE
        [RequesterId] = @RequesterId
        AND [CipherId] = @CipherId
        AND [Action] = 0 -- None (no early end)
        AND [NotBefore] <= @Now
        AND [NotAfter] > @Now
    ORDER BY
        [NotAfter] DESC
END
