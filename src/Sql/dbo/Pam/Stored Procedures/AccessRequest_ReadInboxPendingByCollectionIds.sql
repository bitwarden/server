CREATE PROCEDURE [dbo].[AccessRequest_ReadInboxPendingByCollectionIds]
    @CollectionIds [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    -- The approver inbox: pending requests for the supplied (caller-manageable) collections, joined with the
    -- denormalized requester identity the client needs so it avoids an N+1. A pending request has not been decided by
    -- anyone yet, so it carries no approvers (the caller leaves the request's approvers list empty); only the resolved
    -- reads return a second decision result set. ProducedLease is joined for shape parity with the other request
    -- projections (and the EF read) -- a lease is only ever minted from an Approved request, so it is always NULL here.
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
        PL.[Status] AS [ProducedLeaseStatus],
        U.[Name] AS [RequesterName],
        U.[Email] AS [RequesterEmail]
    FROM [dbo].[AccessRequest] LR
    INNER JOIN @CollectionIds CI ON CI.[Id] = LR.[CollectionId]
    LEFT JOIN [dbo].[User] U ON U.[Id] = LR.[RequesterId]
    LEFT JOIN [dbo].[AccessLease] PL ON PL.[AccessRequestId] = LR.[Id]
    WHERE LR.[Status] = 0 -- Pending
END
