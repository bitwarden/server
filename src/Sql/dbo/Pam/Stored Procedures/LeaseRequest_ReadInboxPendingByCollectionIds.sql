CREATE PROCEDURE [dbo].[LeaseRequest_ReadInboxPendingByCollectionIds]
    @CollectionIds [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    -- The approver inbox: pending requests for the supplied (caller-manageable) collections, joined with the
    -- denormalized display fields the client needs (cipher/collection names, requester identity) so it avoids an N+1.
    -- ResolverId/ResolverComment come from the EARLIEST human decision so a later revocation decision (also human,
    -- recorded against the same request) never overwrites the original approve/deny resolver. ProducedLeaseId is the
    -- lease that the request birthed, if any. ExtensionOfLeaseId is the parent lease for extension requests.
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
    WHERE LR.[Status] = 0 -- Pending
END
