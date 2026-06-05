CREATE PROCEDURE [dbo].[LeaseRequest_ReadInboxHistoryByCollectionIds]
    @CollectionIds [dbo].[GuidIdArray] READONLY,
    @Since DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    -- The approver history: resolved requests (anything no longer Pending) created on or after @Since, for the
    -- supplied (caller-manageable) collections. Same projection as the pending inbox. History rows that produced a
    -- lease carry ProducedLeaseId so the client can target the Revoke action at the lease.
    SELECT
        LR.[Id],
        LR.[LeaseId] AS [ExtensionOfLeaseId],
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
        RES.[ApproverId] AS [ResolverId],
        RES.[Comment] AS [ResolverComment],
        JSON_VALUE(C.[Data], '$.Name') AS [CipherName],
        COL.[Name] AS [CollectionName],
        U.[Name] AS [RequesterName],
        U.[Email] AS [RequesterEmail]
    FROM [dbo].[LeaseRequest] LR
    INNER JOIN @CollectionIds CI ON CI.[Id] = LR.[CollectionId]
    LEFT JOIN [dbo].[Cipher] C ON C.[Id] = LR.[CipherId]
    LEFT JOIN [dbo].[Collection] COL ON COL.[Id] = LR.[CollectionId]
    LEFT JOIN [dbo].[User] U ON U.[Id] = LR.[RequesterId]
    OUTER APPLY (
        SELECT TOP 1 L.[Id]
        FROM [dbo].[Lease] L
        WHERE L.[LeaseRequestId] = LR.[Id]
        ORDER BY L.[CreationDate] DESC
    ) PL
    OUTER APPLY (
        SELECT TOP 1 LD.[ApproverId], LD.[Comment]
        FROM [dbo].[LeaseDecision] LD
        WHERE LD.[LeaseRequestId] = LR.[Id] AND LD.[DeciderKind] = 1 -- Human
        ORDER BY LD.[CreationDate] ASC
    ) RES
    WHERE LR.[Status] <> 0 -- not Pending
        AND LR.[CreationDate] >= @Since
END
