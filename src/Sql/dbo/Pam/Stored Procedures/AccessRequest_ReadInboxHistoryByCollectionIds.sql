CREATE PROCEDURE [dbo].[AccessRequest_ReadInboxHistoryByCollectionIds]
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
