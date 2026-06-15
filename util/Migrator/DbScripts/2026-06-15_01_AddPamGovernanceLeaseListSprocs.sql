-- PAM Credential Leasing: governance lease read models. Two new sprocs back GET /leases/active and
-- GET /leases/history, which list all active / all recently-ended leases on the collections the caller can Manage
-- (resolved the same way as the approver inbox), powering the governance dashboard. Both take the caller's
-- manageable collection ids as a GuidIdArray TVP, mirroring [AccessRequest_ReadInboxPendingByCollectionIds] /
-- [AccessRequest_ReadInboxHistoryByCollectionIds].
--
-- Feature is behind the pm-37044-pam-v-0 flag (unshipped POC); server + migration deploy together.

CREATE OR ALTER PROCEDURE [dbo].[AccessLease_ReadManyActiveByCollectionIds]
    @CollectionIds [dbo].[GuidIdArray] READONLY,
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    -- Every currently-active lease (Active, window containing @Now) on the supplied (caller-manageable) collections,
    -- across all members -- not just the caller's own.
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
GO

CREATE OR ALTER PROCEDURE [dbo].[AccessLease_ReadManyEndedByCollectionIds]
    @CollectionIds [dbo].[GuidIdArray] READONLY,
    @Since DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    -- Leases that have ended (Expired or Revoked) on the supplied (caller-manageable) collections, that ended on or
    -- after @Since. A revoked lease's end is its RevokedDate; an expired lease's end is its NotAfter. Most recently
    -- ended first.
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
GO
