CREATE PROCEDURE [dbo].[LeaseRequest_ReadManyByRequesterId]
    @RequesterId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    -- The caller's own requests across every org, all statuses. Unlike the approver-inbox reads this is a
    -- caller-scoped self-read, so the cipher/collection/requester display-name joins are intentionally omitted
    -- (those name fields stay null). Capped at the 250 most recent; the client renders far fewer.
    SELECT TOP (250)
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
        RES.[Comment] AS [ResolverComment]
    FROM [dbo].[LeaseRequest] LR
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
    WHERE LR.[RequesterId] = @RequesterId
    ORDER BY LR.[CreationDate] DESC
END
