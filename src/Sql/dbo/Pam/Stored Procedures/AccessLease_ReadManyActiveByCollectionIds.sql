CREATE PROCEDURE [dbo].[AccessLease_ReadManyActiveByCollectionIds]
    @CollectionIds [dbo].[GuidIdArray] READONLY,
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    -- Governance view: every currently-active lease (Active, window containing @Now) on the supplied
    -- (caller-manageable) collections, across all members -- not just the caller's own.
    SELECT
        L.*
    FROM
        [dbo].[AccessLease] L
        INNER JOIN @CollectionIds CI ON CI.[Id] = L.[CollectionId]
    WHERE
        L.[Status] = 0 -- Active
        AND L.[NotBefore] <= @Now
        AND L.[NotAfter] > @Now
    ORDER BY
        L.[NotAfter] ASC
END
