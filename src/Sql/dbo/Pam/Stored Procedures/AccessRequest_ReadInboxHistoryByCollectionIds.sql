CREATE PROCEDURE [dbo].[AccessRequest_ReadInboxHistoryByCollectionIds]
    @CollectionIds [dbo].[GuidIdArray] READONLY,
    @Since DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    -- The approver history: resolved requests (anything no longer Pending) created on or after @Since, for the
    -- supplied (caller-manageable) collections. Same projection as the pending inbox. History rows that produced a
    -- lease carry ProducedLeaseId so the client can target the Revoke action at the lease, plus ProducedLeaseStatus
    -- so the client can tell a still-live lease from one that has ended (and not offer Revoke on an ended lease).
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
