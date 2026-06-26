CREATE PROCEDURE [dbo].[AccessRequest_ReadDetailsById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    -- A single access request projected for the dedicated request page, returned as two result sets so the caller can
    -- attach the request's full decision list without an N+1:
    --   1) the request row with the denormalized requester identity. A row that produced a lease carries
    --      ProducedLeaseId/ProducedLeaseStatus so the client can show (and gate) lease actions.
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
        PL.[Id] AS [ProducedLeaseId],
        PL.[Status] AS [ProducedLeaseStatus],
        U.[Name] AS [RequesterName],
        U.[Email] AS [RequesterEmail]
    FROM [dbo].[AccessRequest] LR
    LEFT JOIN [dbo].[User] U ON U.[Id] = LR.[RequesterId]
    OUTER APPLY (
        SELECT TOP 1 L.[Id], L.[Status]
        FROM [dbo].[AccessLease] L
        WHERE L.[AccessRequestId] = LR.[Id]
        ORDER BY L.[CreationDate] DESC
    ) PL
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
