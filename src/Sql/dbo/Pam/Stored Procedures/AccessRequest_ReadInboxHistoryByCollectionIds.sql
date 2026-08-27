CREATE PROCEDURE [dbo].[AccessRequest_ReadInboxHistoryByCollectionIds]
    @CollectionIds [dbo].[GuidIdArray] READONLY,
    @Since DATETIME2(7),
    @Now DATETIME2(7) = NULL
AS
BEGIN
    SET NOCOUNT ON

    -- @Now defaults so a rolling deployment stays safe: an older server that predates this parameter calls the
    -- procedure without it and gets the database clock, which filters the same way.
    SET @Now = COALESCE(@Now, GETUTCDATE())

    -- The approver history, returned as two result sets so the caller can attach each request's full decision list
    -- without an N+1:
    --   1) the non-actionable requests -- an action recorded, or a window lapsed with none (derived Expired); the
    --      exact complement of the pending inbox read -- created on or after @Since, for the supplied
    --      (caller-manageable) collections, with the denormalized requester identity. Rows that produced a lease
    --      carry the lease's id and raw columns so the client can target (and gate) the Revoke action; a request
    --      produces at most one lease ([IX_AccessLease_AccessRequestId] is unique), so that join adds at most one
    --      row. Derived statuses are computed at the repository boundary -- see AccessRequest_ReadDetailsById.
    --   2) every decision (human or automatic) for those requests, keyed by AccessRequestId and ordered oldest-first;
    --      DeciderKind says which, and a human decision's identity is denormalized from [User].
    --
    -- The qualifying ids are materialized once so the history predicate is written once and both result sets are
    -- bounded by exactly the same rows -- the request list and its decision list cannot drift.
    DECLARE @RequestIds TABLE ([Id] UNIQUEIDENTIFIER PRIMARY KEY)

    INSERT INTO @RequestIds ([Id])
    SELECT LR.[Id]
    FROM [dbo].[AccessRequest] LR
    INNER JOIN @CollectionIds CI ON CI.[Id] = LR.[CollectionId]
    WHERE (LR.[Action] <> 0 OR LR.[NotAfter] <= @Now) -- action recorded, or expired unanswered
        AND LR.[CreationDate] >= @Since

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
        LR.[Action],
        LR.[CreationDate],
        LR.[ActionDate],
        LR.[RuleId],
        PL.[Id] AS [ProducedLeaseId],
        PL.[Action] AS [ProducedLeaseAction],
        PL.[NotAfter] AS [ProducedLeaseNotAfter],
        U.[Name] AS [RequesterName],
        U.[Email] AS [RequesterEmail]
    FROM [dbo].[AccessRequest] LR
    INNER JOIN @RequestIds RI ON RI.[Id] = LR.[Id]
    LEFT JOIN [dbo].[User] U ON U.[Id] = LR.[RequesterId]
    LEFT JOIN [dbo].[AccessLease] PL ON PL.[AccessRequestId] = LR.[Id]

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
