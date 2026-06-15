CREATE PROCEDURE [dbo].[AccessLease_ReadManyEndedByCollectionIds]
    @CollectionIds [dbo].[GuidIdArray] READONLY,
    @Since DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    -- Governance history: leases that have ended (Expired or Revoked) on the supplied (caller-manageable)
    -- collections, that ended on or after @Since. A revoked lease's end is its RevokedDate; an expired lease's end
    -- is its NotAfter. Most recently ended first.
    SELECT
        L.*
    FROM
        [dbo].[AccessLease] L
        INNER JOIN @CollectionIds CI ON CI.[Id] = L.[CollectionId]
    WHERE
        L.[Status] IN (1, 2) -- Expired, Revoked
        AND (
            (L.[Status] = 2 AND L.[RevokedDate] >= @Since) -- Revoked
            OR (L.[Status] = 1 AND L.[NotAfter] >= @Since) -- Expired
        )
    ORDER BY
        CASE WHEN L.[Status] = 2 THEN L.[RevokedDate] ELSE L.[NotAfter] END DESC
END
