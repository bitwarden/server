-- PM-42614: hold the requester's own request history to the same retention window as the approver's.
--
-- [AccessRequest_ReadInboxHistoryByCollectionIds] bounds the approver-side history at 90 days; this read had no
-- window at all, only a TOP 250 cap. The same resolved request was therefore visible to the member who raised it
-- indefinitely and invisible to the approvers who decided it after day 90 -- the shorter memory sitting on the
-- governance side, which is backwards. Both reads now share one window.
--
-- Live rows are exempt from it. A Pending request has not been answered and an Approved one whose window is still
-- open can still be activated, so neither is history. This matters in practice, not just in principle: nothing
-- writes status Expired for an unanswered request (there is no sweeper yet), so a Pending row can sit past 90 days,
-- and windowing it away would delete a live request from the requester's own page rather than age out its history.
--
-- @Since is optional so a rolling deployment stays safe: an older server that predates the parameter omits it and
-- gets NULL, which means no window -- the behaviour it was written against. Read-only change: no schema, no data
-- migration, and the TOP 250 cap is unchanged.


CREATE OR ALTER PROCEDURE [dbo].[AccessRequest_ReadManyByRequesterId]
    @RequesterId UNIQUEIDENTIFIER,
    @Now DATETIME2(7) = NULL,
    @Since DATETIME2(7) = NULL
AS
BEGIN
    SET NOCOUNT ON

    -- @Now and @Since default so a rolling deployment stays safe: an older server that predates these parameters
    -- calls the procedure without them, and gets the database clock for @Now plus a NULL @Since, which means no
    -- window -- exactly the behaviour it was written against.
    SET @Now = COALESCE(@Now, GETUTCDATE())

    -- The caller's own requests, returned as two result sets so the caller can attach each request's decision list
    -- without an N+1:
    --   1) the caller's requests (TOP 250 most recent). Unlike the approver-inbox reads this is a caller-scoped
    --      self-read, so the cipher/collection/requester display-name joins are intentionally omitted (those names
    --      come from the caller's local vault, and the requester is the caller).
    --   2) every decision (human or automatic) on those requests, keyed by AccessRequestId and ordered oldest-first;
    --      DeciderKind says which, and a human decision's identity is denormalized from [User] -- the requester has no
    --      other way to name who decided their request.
    --
    -- @Since holds the resolved rows to the same retention window the approver-side history reads use, so the same
    -- resolved request does not outlive itself on one surface and vanish from the other (PM-42614). Live rows are
    -- exempt: a Pending request has not been answered yet and an Approved one whose window is still open can still be
    -- activated, so neither is history and neither ages out. That exemption is load-bearing rather than defensive --
    -- nothing writes status Expired for an unanswered request (there is no sweeper), so a Pending row really can sit
    -- past the window, and windowing it away would drop a live request out of the caller's own list.
    --
    -- The page of ids is materialized first so both result sets are bounded by the same 250 rows. Selecting decisions
    -- straight from [RequesterId] would return the caller's entire decision history for the caller to then discard
    -- everything outside the page.
    DECLARE @RequestIds TABLE ([Id] UNIQUEIDENTIFIER PRIMARY KEY)

    INSERT INTO @RequestIds ([Id])
    SELECT TOP (250) [Id]
    FROM [dbo].[AccessRequest]
    WHERE [RequesterId] = @RequesterId
        AND (
            @Since IS NULL
            OR [CreationDate] >= @Since
            OR [Status] = 0 -- Pending: awaiting a decision
            OR ([Status] = 1 AND [NotAfter] > @Now) -- Approved with an unlapsed window
        )
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
