CREATE PROCEDURE [dbo].[AccessRequest_ReadDetailsById]
    @Id UNIQUEIDENTIFIER,
    @Now DATETIME2(7) = NULL
AS
BEGIN
    SET NOCOUNT ON

    -- @Now is accepted and unused: callers pass the read clock (older servers unconditionally), but nothing here
    -- consults it any more -- only stored facts leave this read, and the derived statuses are computed against the
    -- caller's clock at the repository boundary.

    -- A single access request projected for the dedicated request page, returned as two result sets so the caller can
    -- attach the request's full decision list without an N+1:
    --   1) the request row with the denormalized requester identity. A row that produced a lease carries the lease's
    --      id and raw columns so the client can show (and gate) lease actions; a request produces at most one lease
    --      ([IX_AccessLease_AccessRequestId] is unique), so that join adds at most one row.
    --   2) every decision (human or automatic) for the request, keyed by AccessRequestId and ordered oldest-first;
    --      DeciderKind says which, and a human decision's identity is denormalized from [User].
    -- Only stored facts leave this read: the derived request status and the produced lease's derived status are
    -- computed at the repository boundary against the caller's clock, off [Action]/[NotAfter] and the lease's own
    -- [Action]/[NotAfter] (an extension pushes the lease's end out in place, so the request's [NotAfter] would report
    -- a live lease as expired).
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
