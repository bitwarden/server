CREATE PROCEDURE [dbo].[AccessLease_ReadActiveByCipherId]
    @CipherId UNIQUEIDENTIFIER,
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    -- Latest-ending across all members, since the singleton guard blocks until the last live lease frees the slot.
    SELECT TOP 1
        *
    FROM
        [dbo].[AccessLease]
    WHERE
        [CipherId] = @CipherId
        AND [Action] = 0 -- None (no early end)
        AND [NotBefore] <= @Now
        AND [NotAfter] > @Now
    ORDER BY
        [NotAfter] DESC
END
