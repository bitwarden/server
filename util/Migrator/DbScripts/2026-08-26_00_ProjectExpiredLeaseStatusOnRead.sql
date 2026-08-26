-- PM-42355: project a lapsed lease's status as Expired on read.
--
-- [dbo].[AccessLease].[Status] only ever records an *early* end (Revoked / Cancelled). Nothing writes Expired, so a
-- lease whose window simply closed keeps Status 0 (Active) in the table forever, and every read that handed the
-- column out raw reported an ended lease as still active -- the request-detail and request-list projections, and
-- (worse) the ended-lease governance history, whose [Status] IN (1, 2, 3) filter matched no naturally expired lease
-- at all and so hid every one of them.
--
-- These four reads now derive the status against a clock instead. @Now is optional on all of them so a rolling
-- deployment stays safe: an older server that predates the parameter omits it and gets GETUTCDATE(), which projects
-- the same result. Read-only change -- no schema, no data migration; the stored column keeps its existing meaning.


CREATE OR ALTER PROCEDURE [dbo].[AccessRequest_ReadDetailsById]
    @Id UNIQUEIDENTIFIER,
    @Now DATETIME2(7) = NULL
AS
BEGIN
    SET NOCOUNT ON

    -- @Now defaults so a rolling deployment stays safe: an older server that predates this parameter calls the
    -- procedure without it and gets the database clock, which projects the same result.
    SET @Now = COALESCE(@Now, GETUTCDATE())

    -- A single access request projected for the dedicated request page, returned as two result sets so the caller can
    -- attach the request's full decision list without an N+1:
    --   1) the request row with the denormalized requester identity. A row that produced a lease carries
    --      ProducedLeaseId/ProducedLeaseStatus so the client can show (and gate) lease actions; a request produces at
    --      most one lease ([IX_AccessLease_AccessRequestId] is unique), so that join adds at most one row.
    --      ProducedLeaseStatus is projected against @Now -- see the CASE below.
    --   2) every decision (human or automatic) for the request, keyed by AccessRequestId and ordered oldest-first;
    --      DeciderKind says which, and a human decision's identity is denormalized from [User].
    -- Authorization (requester or managing approver) is enforced by the caller, not this read.
    SELECT
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
        LR.[RuleId],
        PL.[Id] AS [ProducedLeaseId],
        -- Expired is never stored: [AccessLease].[Status] only records an early end, so a lease whose window closed
        -- stays 0 (Active) in the table forever. Derive it here against @Now, off the LEASE's NotAfter rather than
        -- the request's -- an extension pushes the lease's end out in place and leaves the original request row
        -- behind (see AccessRequest_CreateApprovedExtension), so the request's NotAfter would report a live lease
        -- as expired. Mirrors AccessLease.StatusAsOf.
        CASE WHEN PL.[Status] = 0 AND PL.[NotAfter] <= @Now THEN 1 ELSE PL.[Status] END AS [ProducedLeaseStatus],
        U.[Name] AS [RequesterName],
        U.[Email] AS [RequesterEmail]
    FROM [dbo].[AccessRequest] LR
    LEFT JOIN [dbo].[User] U ON U.[Id] = LR.[RequesterId]
    LEFT JOIN [dbo].[AccessLease] PL ON PL.[AccessRequestId] = LR.[Id]
    WHERE LR.[Id] = @Id

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
    LEFT JOIN [dbo].[User] AU ON AU.[Id] = AD.[ApproverId]
    WHERE AD.[AccessRequestId] = @Id
    ORDER BY AD.[CreationDate] ASC
END
GO

CREATE OR ALTER PROCEDURE [dbo].[AccessRequest_ReadManyByRequesterId]
    @RequesterId UNIQUEIDENTIFIER,
    @Now DATETIME2(7) = NULL
AS
BEGIN
    SET NOCOUNT ON

    -- @Now defaults so a rolling deployment stays safe: an older server that predates this parameter calls the
    -- procedure without it and gets the database clock, which projects the same result.
    SET @Now = COALESCE(@Now, GETUTCDATE())

    -- The caller's own requests, returned as two result sets so the caller can attach each request's decision list
    -- without an N+1:
    --   1) the caller's requests (TOP 250 most recent), all statuses. Unlike the approver-inbox reads this is a
    --      caller-scoped self-read, so the cipher/collection/requester display-name joins are intentionally omitted
    --      (those names come from the caller's local vault, and the requester is the caller).
    --   2) every decision (human or automatic) on those requests, keyed by AccessRequestId and ordered oldest-first;
    --      DeciderKind says which, and a human decision's identity is denormalized from [User] -- the requester has no
    --      other way to name who decided their request.
    --
    -- The page of ids is materialized first so both result sets are bounded by the same 250 rows. Selecting decisions
    -- straight from [RequesterId] would return the caller's entire decision history for the caller to then discard
    -- everything outside the page.
    DECLARE @RequestIds TABLE ([Id] UNIQUEIDENTIFIER PRIMARY KEY)

    INSERT INTO @RequestIds ([Id])
    SELECT TOP (250) [Id]
    FROM [dbo].[AccessRequest]
    WHERE [RequesterId] = @RequesterId
    ORDER BY [CreationDate] DESC

    -- A request produces at most one lease ([IX_AccessLease_AccessRequestId] is unique), so this joins at most one row.
    -- ProducedLeaseStatus is projected against @Now, as in AccessRequest_ReadDetailsById.
    SELECT
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
        LR.[RuleId],
        PL.[Id] AS [ProducedLeaseId],
        -- Expired is never stored; derive it against @Now off the lease's own NotAfter. See
        -- AccessRequest_ReadDetailsById for why the request's NotAfter will not do.
        CASE WHEN PL.[Status] = 0 AND PL.[NotAfter] <= @Now THEN 1 ELSE PL.[Status] END AS [ProducedLeaseStatus]
    FROM [dbo].[AccessRequest] LR
    INNER JOIN @RequestIds RI ON RI.[Id] = LR.[Id]
    LEFT JOIN [dbo].[AccessLease] PL ON PL.[AccessRequestId] = LR.[Id]
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
    INNER JOIN @RequestIds RI ON RI.[Id] = AD.[AccessRequestId]
    LEFT JOIN [dbo].[User] AU ON AU.[Id] = AD.[ApproverId]
    ORDER BY AD.[AccessRequestId], AD.[CreationDate] ASC
END
GO

CREATE OR ALTER PROCEDURE [dbo].[AccessRequest_ReadInboxHistoryByCollectionIds]
    @CollectionIds [dbo].[GuidIdArray] READONLY,
    @Since DATETIME2(7),
    @Now DATETIME2(7) = NULL
AS
BEGIN
    SET NOCOUNT ON

    -- @Now defaults so a rolling deployment stays safe: an older server that predates this parameter calls the
    -- procedure without it and gets the database clock, which projects the same result.
    SET @Now = COALESCE(@Now, GETUTCDATE())

    -- The approver history, returned as two result sets so the caller can attach each request's full decision list
    -- without an N+1:
    --   1) the resolved requests (anything no longer Pending) created on or after @Since, for the supplied
    --      (caller-manageable) collections, with the denormalized requester identity. Rows that produced a lease carry
    --      ProducedLeaseId/ProducedLeaseStatus so the client can target (and gate) the Revoke action; a request
    --      produces at most one lease ([IX_AccessLease_AccessRequestId] is unique), so that join adds at most one row.
    --      ProducedLeaseStatus is projected against @Now, as in AccessRequest_ReadDetailsById.
    --   2) every decision (human or automatic) for those requests, keyed by AccessRequestId and ordered oldest-first;
    --      DeciderKind says which, and a human decision's identity is denormalized from [User].
    SELECT
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
        LR.[RuleId],
        PL.[Id] AS [ProducedLeaseId],
        -- Expired is never stored; derive it against @Now off the lease's own NotAfter. See
        -- AccessRequest_ReadDetailsById for why the request's NotAfter will not do.
        CASE WHEN PL.[Status] = 0 AND PL.[NotAfter] <= @Now THEN 1 ELSE PL.[Status] END AS [ProducedLeaseStatus],
        U.[Name] AS [RequesterName],
        U.[Email] AS [RequesterEmail]
    FROM [dbo].[AccessRequest] LR
    INNER JOIN @CollectionIds CI ON CI.[Id] = LR.[CollectionId]
    LEFT JOIN [dbo].[User] U ON U.[Id] = LR.[RequesterId]
    LEFT JOIN [dbo].[AccessLease] PL ON PL.[AccessRequestId] = LR.[Id]
    WHERE LR.[Status] <> 0 -- not Pending
        AND LR.[CreationDate] >= @Since

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
    INNER JOIN @CollectionIds CI ON CI.[Id] = LR.[CollectionId]
    LEFT JOIN [dbo].[User] AU ON AU.[Id] = AD.[ApproverId]
    WHERE LR.[Status] <> 0 -- not Pending
        AND LR.[CreationDate] >= @Since
    ORDER BY AD.[AccessRequestId], AD.[CreationDate] ASC
END
GO

CREATE OR ALTER PROCEDURE [dbo].[AccessLease_ReadManyEndedByCollectionIds]
    @CollectionIds [dbo].[GuidIdArray] READONLY,
    @Since DATETIME2(7),
    @Now DATETIME2(7) = NULL
AS
BEGIN
    SET NOCOUNT ON

    -- @Now defaults so a rolling deployment stays safe: an older server that predates this parameter calls the
    -- procedure without it and gets the database clock, which projects the same result.
    SET @Now = COALESCE(@Now, GETUTCDATE())

    -- Governance history: leases that have ended (Expired, Revoked, or Cancelled) on the supplied (caller-manageable)
    -- collections, that ended on or after @Since. A revoked/cancelled lease's end is its RevokedDate; an expired
    -- lease's end is its NotAfter. Most recently ended first.
    --
    -- "Ended" has to be derived, not read: [Status] only ever records an early end, so a lease whose window simply
    -- closed keeps Status 0 (Active) forever. Filtering on [Status] IN (1, 2, 3) therefore matched no naturally
    -- expired lease at all, and every one of them fell out of this view entirely -- out of the active read (its
    -- window has closed) and never into this one (PM-42355). Both the filter and the projected [Status] below
    -- reinterpret a closed-window Active row as Expired, mirroring AccessLease.StatusAsOf.
    SELECT
        L.[Id],
        L.[AccessRequestId],
        L.[OrganizationId],
        L.[CollectionId],
        L.[CipherId],
        L.[RequesterId],
        CASE WHEN L.[Status] = 0 AND L.[NotAfter] <= @Now THEN 1 ELSE L.[Status] END AS [Status],
        L.[NotBefore],
        L.[NotAfter],
        L.[RevokedDate],
        L.[RevokedBy],
        L.[CreationDate]
    FROM
        [dbo].[AccessLease] L
        INNER JOIN @CollectionIds CI ON CI.[Id] = L.[CollectionId]
    WHERE
        -- Ended early (Revoked, Cancelled): its end is RevokedDate, whatever its window says.
        (L.[Status] IN (2, 3) AND L.[RevokedDate] >= @Since)
        -- Window closed: its end is NotAfter. Status 0 is the case that was missing; Status 1 stays matched so a
        -- row that does carry a stored Expired is still read.
        OR (L.[Status] IN (0, 1) AND L.[NotAfter] <= @Now AND L.[NotAfter] >= @Since)
    ORDER BY
        CASE WHEN L.[Status] IN (2, 3) THEN L.[RevokedDate] ELSE L.[NotAfter] END DESC
END
GO
