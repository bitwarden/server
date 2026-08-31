CREATE PROCEDURE [dbo].[AccessLease_ReadActiveByCipherId]
    @CipherId UNIQUEIDENTIFIER,
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    -- Does *anyone* currently hold this cipher's lease, and when does it free? Same predicate and same cipher-only
    -- scope as the singleton guard in [AccessLease_CreateFromApprovedRequest]; latest-ending first, because that
    -- guard blocks while ANY in-window lease exists. See IAccessLeaseRepository.GetActiveByCipherIdAsync for why
    -- both of those matter.
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
