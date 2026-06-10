-- PAM member read endpoints: caller-scoped reads for the cipher-lease banner, vault-row badge, and the
-- "My access requests" page. Lease_ReadManyActiveByRequesterId backs "my active leases";
-- AccessRequest_ReadManyByRequesterId backs "my requests" (all statuses, names omitted — caller-scoped self-read).

CREATE OR ALTER PROCEDURE [dbo].[AccessLease_ReadManyActiveByRequesterId]
    @RequesterId UNIQUEIDENTIFIER,
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[AccessLease]
    WHERE
        [RequesterId] = @RequesterId
        AND [Status] = 0 -- Active
        AND [NotBefore] <= @Now
        AND [NotAfter] > @Now
    ORDER BY
        [NotAfter] ASC
END
GO

CREATE OR ALTER PROCEDURE [dbo].[AccessRequest_ReadManyByRequesterId]
    @RequesterId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    -- The caller's own requests across every org, all statuses. Unlike the approver-inbox reads this is a
    -- caller-scoped self-read, so the cipher/collection/requester display-name joins are intentionally omitted
    -- (those name fields stay null). Capped at the 250 most recent; the client renders far fewer.
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
        RES.[Comment] AS [ApproverComment]
    FROM [dbo].[AccessRequest] LR
    OUTER APPLY (
        SELECT TOP 1 L.[Id]
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
    WHERE LR.[RequesterId] = @RequesterId
    ORDER BY LR.[CreationDate] DESC
END
GO
