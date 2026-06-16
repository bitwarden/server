-- PAM access-request reads: resolve the human approver's identity (name + email) alongside the existing ApproverId.
-- The reads already surfaced the resolving approver's id, but only as a raw GUID; the caller's own request list
-- ("My Requests") and the approver-inbox audit log had no way to name who decided a request. Each read resolves the
-- approver inside the existing human-decision OUTER APPLY (LEFT JOIN to [User] on the decision's ApproverId), so the
-- new ApproverName/ApproverEmail follow the same earliest-human-decision row as ApproverId/ApproverComment.

CREATE OR ALTER PROCEDURE [dbo].[AccessRequest_ReadManyByRequesterId]
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
GO

CREATE OR ALTER PROCEDURE [dbo].[AccessRequest_ReadInboxPendingByCollectionIds]
    @CollectionIds [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

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
        RES.[ApproverId] AS [ApproverId],
        RES.[ApproverName] AS [ApproverName],
        RES.[ApproverEmail] AS [ApproverEmail],
        RES.[Comment] AS [ApproverComment],
        JSON_VALUE(C.[Data], '$.Name') AS [CipherName],
        COL.[Name] AS [CollectionName],
        U.[Name] AS [RequesterName],
        U.[Email] AS [RequesterEmail]
    FROM [dbo].[AccessRequest] LR
    INNER JOIN @CollectionIds CI ON CI.[Id] = LR.[CollectionId]
    LEFT JOIN [dbo].[Cipher] C ON C.[Id] = LR.[CipherId]
    LEFT JOIN [dbo].[Collection] COL ON COL.[Id] = LR.[CollectionId]
    LEFT JOIN [dbo].[User] U ON U.[Id] = LR.[RequesterId]
    OUTER APPLY (
        SELECT TOP 1 L.[Id], L.[Status]
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
    WHERE LR.[Status] = 0 -- Pending
END
GO

CREATE OR ALTER PROCEDURE [dbo].[AccessRequest_ReadInboxHistoryByCollectionIds]
    @CollectionIds [dbo].[GuidIdArray] READONLY,
    @Since DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

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
        RES.[ApproverId] AS [ApproverId],
        RES.[ApproverName] AS [ApproverName],
        RES.[ApproverEmail] AS [ApproverEmail],
        RES.[Comment] AS [ApproverComment],
        JSON_VALUE(C.[Data], '$.Name') AS [CipherName],
        COL.[Name] AS [CollectionName],
        U.[Name] AS [RequesterName],
        U.[Email] AS [RequesterEmail]
    FROM [dbo].[AccessRequest] LR
    INNER JOIN @CollectionIds CI ON CI.[Id] = LR.[CollectionId]
    LEFT JOIN [dbo].[Cipher] C ON C.[Id] = LR.[CipherId]
    LEFT JOIN [dbo].[Collection] COL ON COL.[Id] = LR.[CollectionId]
    LEFT JOIN [dbo].[User] U ON U.[Id] = LR.[RequesterId]
    OUTER APPLY (
        SELECT TOP 1 L.[Id], L.[Status]
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
    WHERE LR.[Status] <> 0 -- not Pending
        AND LR.[CreationDate] >= @Since
END
GO
