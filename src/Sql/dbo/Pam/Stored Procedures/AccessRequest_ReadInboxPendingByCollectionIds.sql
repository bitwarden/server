CREATE PROCEDURE [dbo].[AccessRequest_ReadInboxPendingByCollectionIds]
    @CollectionIds [dbo].[GuidIdArray] READONLY,
    @Now DATETIME2(7) = NULL
AS
BEGIN
    SET NOCOUNT ON

    -- @Now defaults so a rolling deployment stays safe: an older server that predates this parameter calls the
    -- procedure without it and gets the database clock, which filters the same way.
    SET @Now = COALESCE(@Now, GETUTCDATE())

    -- The approver inbox: actionable requests for the supplied (caller-manageable) collections, joined with the
    -- denormalized requester identity the client needs so it avoids an N+1. Actionable means no action recorded AND a
    -- window still open -- a lapsed unanswered row is derived Expired, leaves this inbox, and lands in the history
    -- read instead. An open request has not been decided by anyone yet, so it carries no approvers (the caller leaves
    -- the request's approvers list empty); only the resolved reads return a second decision result set. No AccessLease
    -- join: a lease is only ever minted from an approved request, so an open row cannot have one -- the produced-lease
    -- columns are simply absent and hydrate as no-lease (the EF read skips the same lookup for the same reason).
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
        U.[Name] AS [RequesterName],
        U.[Email] AS [RequesterEmail]
    FROM [dbo].[AccessRequest] LR
    INNER JOIN @CollectionIds CI ON CI.[Id] = LR.[CollectionId]
    LEFT JOIN [dbo].[User] U ON U.[Id] = LR.[RequesterId]
    WHERE LR.[Action] = 0 -- None (open)
        AND LR.[NotAfter] > @Now
END
