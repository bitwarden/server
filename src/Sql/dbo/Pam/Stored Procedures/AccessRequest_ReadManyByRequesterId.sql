CREATE PROCEDURE [dbo].[AccessRequest_ReadManyByRequesterId]
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
