CREATE PROCEDURE [dbo].[AccessRequest_ReadManyByRequesterId]
    @RequesterId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    -- The caller's own requests across every org, all statuses. Unlike the approver-inbox reads this is a
    -- caller-scoped self-read, so the cipher/collection/requester display-name joins are intentionally omitted
    -- (those name fields stay null) -- cipher/collection names come from the caller's local vault, and the
    -- requester is the caller. The approver, however, is resolved here (ApproverName/ApproverEmail) because the
    -- requester has no other way to name who decided their request. Capped at the 250 most recent; the client
    -- renders far fewer.
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
        RES.[ApproverId] AS [ApproverId],
        RES.[ApproverName] AS [ApproverName],
        RES.[ApproverEmail] AS [ApproverEmail],
        RES.[Comment] AS [ApproverComment]
    FROM [dbo].[AccessRequest] LR
    OUTER APPLY (
        SELECT TOP 1 L.[Id]
        FROM [dbo].[AccessLease] L
        WHERE L.[AccessRequestId] = LR.[Id]
        ORDER BY L.[CreationDate] DESC
    ) PL
    OUTER APPLY (
        SELECT TOP 1 LD.[ApproverId], LD.[Comment], AU.[Name] AS [ApproverName], AU.[Email] AS [ApproverEmail]
        FROM [dbo].[AccessDecision] LD
        LEFT JOIN [dbo].[User] AU ON AU.[Id] = LD.[ApproverId]
        WHERE LD.[AccessRequestId] = LR.[Id] AND LD.[DeciderKind] = 1 -- Human
        ORDER BY LD.[CreationDate] ASC
    ) RES
    WHERE LR.[RequesterId] = @RequesterId
    ORDER BY LR.[CreationDate] DESC
END
