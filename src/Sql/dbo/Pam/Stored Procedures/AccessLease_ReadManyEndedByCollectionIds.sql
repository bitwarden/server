CREATE PROCEDURE [dbo].[AccessLease_ReadManyEndedByCollectionIds]
    @CollectionIds [dbo].[GuidIdArray] READONLY,
    @Since DATETIME2(7),
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    -- An early end (Revoked, Cancelled) ends at RevokedDate, a natural close at NotAfter.
    SELECT
        L.[Id],
        L.[AccessRequestId],
        L.[OrganizationId],
        L.[CollectionId],
        L.[CipherId],
        L.[RequesterId],
        L.[Action],
        L.[NotBefore],
        L.[NotAfter],
        L.[RevokedDate],
        L.[RevokedBy],
        L.[CreationDate]
    FROM
        [dbo].[AccessLease] L
        INNER JOIN @CollectionIds CI ON CI.[Id] = L.[CollectionId]
    WHERE
        (L.[Action] IN (2, 3) AND L.[RevokedDate] >= @Since)
        OR (L.[Action] = 0 AND L.[NotAfter] <= @Now AND L.[NotAfter] >= @Since)
    ORDER BY
        CASE WHEN L.[Action] IN (2, 3) THEN L.[RevokedDate] ELSE L.[NotAfter] END DESC
END
