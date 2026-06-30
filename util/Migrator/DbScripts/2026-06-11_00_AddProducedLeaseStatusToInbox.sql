-- PAM Approver Inbox: surface the produced lease's status alongside its id. The inbox previously knew only that a
-- request had produced a lease (ProducedLeaseId), not whether that lease was still Active. A lease ended elsewhere
-- (the requester's "End lease", natural expiry, or another approver) still looked active in the inbox, leaving a
-- clickable Revoke that the server then rejects with 409. Selecting [Status] lets the client keep an ended lease out
-- of the "active" group so Revoke is no longer offered for it.

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
        SELECT TOP 1 LD.[ApproverId], LD.[Comment]
        FROM [dbo].[AccessDecision] LD
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
        SELECT TOP 1 LD.[ApproverId], LD.[Comment]
        FROM [dbo].[AccessDecision] LD
        WHERE LD.[AccessRequestId] = LR.[Id] AND LD.[DeciderKind] = 1 -- Human
        ORDER BY LD.[CreationDate] ASC
    ) RES
    WHERE LR.[Status] <> 0 -- not Pending
        AND LR.[CreationDate] >= @Since
END
GO
