-- PAM: distinguish a holder-ended lease (Cancelled) from an operator-ended one (Revoked).
-- The AccessLeaseStatus enum gains Cancelled = 3. This migration:
--   1) parameterizes AccessLease_Revoke so the end state (Revoked vs Cancelled) is supplied by the caller;
--   2) includes Cancelled in the governance ended-leases history (a self-ended lease must not vanish from it);
--   3) returns the produced lease's Status on the caller's own request read, which previously omitted it (so the
--      "My Requests" history could not tell a revoked/cancelled lease from an expired one, defaulting to Expired).
-- No schema change: a cancelled lease reuses RevokedDate/RevokedBy (when/who ended it); the status carries the manner.

CREATE OR ALTER PROCEDURE [dbo].[AccessLease_Revoke]
    @AccessLeaseId UNIQUEIDENTIFIER,
    @AccessRequestId UNIQUEIDENTIFIER,
    @Status TINYINT,
    @RevokedBy UNIQUEIDENTIFIER,
    @AccessDecisionId UNIQUEIDENTIFIER,
    @Reason NVARCHAR(MAX) = NULL,
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    -- Atomically end an active lease and capture who/why. @Status is the end state: 2 (Revoked) when an operator ended
    -- it, 3 (Cancelled) when the holder ended their own; RevokedDate/RevokedBy record when/who either way. The reason
    -- has no dedicated column, so it is preserved as a human AccessDecision (Deny) against the lease's originating
    -- request, keeping the audit trail without a schema change. The WHERE guard keeps the end idempotent if two
    -- callers race.
    BEGIN TRANSACTION AccessLease_Revoke

    UPDATE [dbo].[AccessLease]
    SET [Status] = @Status,
        [RevokedDate] = @Now,
        [RevokedBy] = @RevokedBy
    WHERE [Id] = @AccessLeaseId AND [Status] = 0 -- Active

    INSERT INTO [dbo].[AccessDecision]
    (
        [Id], [AccessRequestId], [DeciderKind], [ApproverId], [ConditionKind],
        [Verdict], [Comment], [EvaluationContext], [CreationDate]
    )
    VALUES
    (
        @AccessDecisionId, @AccessRequestId, 1 /* Human */, @RevokedBy, NULL,
        0 /* Deny */, @Reason, NULL, @Now
    )

    COMMIT TRANSACTION AccessLease_Revoke
END
GO

CREATE OR ALTER PROCEDURE [dbo].[AccessLease_ReadManyEndedByCollectionIds]
    @CollectionIds [dbo].[GuidIdArray] READONLY,
    @Since DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    -- Governance history: leases that have ended (Expired, Revoked, or Cancelled) on the supplied (caller-manageable)
    -- collections, that ended on or after @Since. A revoked/cancelled lease's end is its RevokedDate; an expired
    -- lease's end is its NotAfter. Most recently ended first.
    SELECT
        L.*
    FROM
        [dbo].[AccessLease] L
        INNER JOIN @CollectionIds CI ON CI.[Id] = L.[CollectionId]
    WHERE
        L.[Status] IN (1, 2, 3) -- Expired, Revoked, Cancelled
        AND (
            (L.[Status] IN (2, 3) AND L.[RevokedDate] >= @Since) -- Revoked, Cancelled
            OR (L.[Status] = 1 AND L.[NotAfter] >= @Since) -- Expired
        )
    ORDER BY
        CASE WHEN L.[Status] IN (2, 3) THEN L.[RevokedDate] ELSE L.[NotAfter] END DESC
END
GO

CREATE OR ALTER PROCEDURE [dbo].[AccessRequest_ReadManyByRequesterId]
    @RequesterId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    -- The caller's own requests, returned as two result sets so the caller can attach each request's decision list
    -- without an N+1:
    --   1) the caller's requests (TOP 250 most recent), all statuses, with the produced lease's id and status so the
    --      "My Requests" history can label an ended lease by its outcome (revoked/cancelled/expired). Unlike the
    --      approver-inbox reads this is a caller-scoped self-read, so the cipher/collection/requester display-name
    --      joins are intentionally omitted (those names come from the caller's local vault, and the requester is the
    --      caller).
    --   2) every decision (human or automatic) on the caller's requests, keyed by AccessRequestId and ordered
    --      oldest-first; DeciderKind says which, and a human decision's identity is denormalized from [User] -- the
    --      requester has no other way to name who decided their request.
    SELECT TOP (250)
        LR.[Id],
        LR.[ExtensionOfLeaseId],
        LR.[OrganizationId],
        LR.[CollectionId],
        LR.[CipherId],
        LR.[RequesterId],
        LR.[NotBefore],
        LR.[NotAfter],
        LR.[Reason],
        LR.[Status],
        LR.[CreationDate],
        LR.[ResolvedDate],
        PL.[Id] AS [ProducedLeaseId],
        PL.[Status] AS [ProducedLeaseStatus]
    FROM [dbo].[AccessRequest] LR
    OUTER APPLY (
        SELECT TOP 1 L.[Id], L.[Status]
        FROM [dbo].[AccessLease] L
        WHERE L.[AccessRequestId] = LR.[Id]
        ORDER BY L.[CreationDate] DESC
    ) PL
    WHERE LR.[RequesterId] = @RequesterId
    ORDER BY LR.[CreationDate] DESC

    SELECT
        AD.[AccessRequestId],
        AD.[DeciderKind] AS [DeciderKind],
        AD.[ApproverId] AS [Id],
        AU.[Name] AS [Name],
        AU.[Email] AS [Email],
        AD.[Comment] AS [Comment],
        AD.[Verdict] AS [Verdict],
        AD.[CreationDate] AS [DecidedAt]
    FROM [dbo].[AccessDecision] AD
    INNER JOIN [dbo].[AccessRequest] LR ON LR.[Id] = AD.[AccessRequestId]
    LEFT JOIN [dbo].[User] AU ON AU.[Id] = AD.[ApproverId]
    WHERE LR.[RequesterId] = @RequesterId
    ORDER BY AD.[AccessRequestId], AD.[CreationDate] ASC
END
GO
