CREATE PROCEDURE [dbo].[AccessLease_ReadManyActiveByCollectionIds]
    @CollectionIds [dbo].[GuidIdArray] READONLY,
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON
    -- Governance view: every active lease across all members in the caller-manageable collections.
    SELECT
        L.*
    FROM
        [dbo].[AccessLease] L
        INNER JOIN @CollectionIds CI ON CI.[Id] = L.[CollectionId]
    WHERE
        L.[Action] = 0 -- None (no early end)
        AND L.[NotBefore] <= @Now
        AND L.[NotAfter] > @Now
    ORDER BY
        L.[NotAfter] ASC
END
