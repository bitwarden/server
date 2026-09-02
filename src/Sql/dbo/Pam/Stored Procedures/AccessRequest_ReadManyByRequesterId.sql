CREATE PROCEDURE [dbo].[AccessRequest_ReadManyByRequesterId]
    @RequesterId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

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
        PL.[Status] AS [ProducedLeaseStatus]
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
