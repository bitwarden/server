-- PAM access-audit trail: make it org-wide, authorized by the AccessEventLogs permission, instead of scoping to the
-- caller's manageable collections. Replaces AccessAuditEvent_ReadManyByCollectionIds (collection-scoped, which hid
-- rule-administration events whose rule did not yet govern a manageable collection) with
-- AccessAuditEvent_ReadManyByOrganizationId (every kind scopes by OrganizationId).

CREATE OR ALTER PROCEDURE [dbo].[AccessAuditEvent_ReadManyByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER,
    @Since DATETIME2(7),
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    -- Synthesizes the PAM access-audit trail for an entire organization: every event occurring on or after @Since,
    -- newest first. There is no audit table -- each row is projected from the lifecycle state the PAM entities already
    -- retain. The trail is org-wide: the caller is authorized by the AccessEventLogs permission (see the audit endpoint),
    -- not by collection management, so every kind scopes by OrganizationId. Covers the access-request, access-lease, and
    -- rule administration kinds; rule events scope by AccessRule.OrganizationId (a rule surfaces from creation, whether
    -- or not it yet governs a collection). Kind codes match Bit.Pam.Enums.AccessAuditEventKind.

    SELECT
        E.[Kind],
        E.[OccurredAt],
        E.[OrganizationId],
        E.[ActorId],
        E.[RequesterId],
        E.[CollectionId],
        E.[CipherId],
        E.[AccessRequestId],
        E.[AccessLeaseId],
        E.[AccessRuleId],
        E.[Detail],
        E.[LeaseNotBefore],
        E.[LeaseNotAfter],
        AU.[Name] AS [ActorName],
        AU.[Email] AS [ActorEmail],
        RU.[Name] AS [RequesterName],
        RU.[Email] AS [RequesterEmail],
        JSON_VALUE(C.[Data], '$.Name') AS [CipherName],
        COL.[Name] AS [CollectionName]
    FROM (
    -- RequestSubmitted: an original (non-extension) request was created; actor = requester.
    SELECT
        CAST(0 AS TINYINT) AS [Kind],
        AR.[CreationDate] AS [OccurredAt],
        AR.[OrganizationId] AS [OrganizationId],
        AR.[RequesterId] AS [ActorId],
        AR.[RequesterId] AS [RequesterId],
        AR.[CollectionId] AS [CollectionId],
        AR.[CipherId] AS [CipherId],
        AR.[Id] AS [AccessRequestId],
        CAST(NULL AS UNIQUEIDENTIFIER) AS [AccessLeaseId],
        CAST(NULL AS UNIQUEIDENTIFIER) AS [AccessRuleId],
        CAST(NULL AS NVARCHAR(MAX)) AS [Detail],
        CAST(NULL AS DATETIME2(7)) AS [LeaseNotBefore],
        CAST(NULL AS DATETIME2(7)) AS [LeaseNotAfter]
    FROM [dbo].[AccessRequest] AR
    WHERE AR.[OrganizationId] = @OrganizationId
        AND AR.[ExtensionOfLeaseId] IS NULL
        AND AR.[CreationDate] >= @Since

    UNION ALL
    -- RequestApproved: every approval (human or automatic) is recorded as an AccessDecision with Verdict = Approve.
    SELECT
        CAST(1 AS TINYINT),
        AD.[CreationDate],
        AR.[OrganizationId],
        AD.[ApproverId],
        AR.[RequesterId],
        AR.[CollectionId],
        AR.[CipherId],
        AR.[Id],
        NULL,
        NULL,
        AD.[Comment],
        NULL,
        NULL
    FROM [dbo].[AccessDecision] AD
    INNER JOIN [dbo].[AccessRequest] AR ON AR.[Id] = AD.[AccessRequestId]
    WHERE AR.[OrganizationId] = @OrganizationId
        AND AR.[ExtensionOfLeaseId] IS NULL          -- extension approvals surface as LeaseExtended instead
        AND AD.[Verdict] = 1 -- Approve
        AND AD.[CreationDate] >= @Since

    UNION ALL
    -- RequestDenied: a Deny decision on a request that ended Denied. A Deny on a still-Approved request is a lease
    -- revoke and surfaces as LeaseRevoked from the lease, not here.
    SELECT
        CAST(2 AS TINYINT),
        AD.[CreationDate],
        AR.[OrganizationId],
        AD.[ApproverId],
        AR.[RequesterId],
        AR.[CollectionId],
        AR.[CipherId],
        AR.[Id],
        NULL,
        NULL,
        AD.[Comment],
        NULL,
        NULL
    FROM [dbo].[AccessDecision] AD
    INNER JOIN [dbo].[AccessRequest] AR ON AR.[Id] = AD.[AccessRequestId]
    WHERE AR.[OrganizationId] = @OrganizationId
        AND AR.[ExtensionOfLeaseId] IS NULL
        AND AD.[Verdict] = 0 -- Deny
        AND AR.[Status] = 2 -- Denied
        AND AD.[CreationDate] >= @Since

    UNION ALL
    -- RequestCancelled: the requester withdrew their own request; no decision is recorded, so actor = requester.
    SELECT
        CAST(3 AS TINYINT),
        AR.[ResolvedDate],
        AR.[OrganizationId],
        AR.[RequesterId],
        AR.[RequesterId],
        AR.[CollectionId],
        AR.[CipherId],
        AR.[Id],
        NULL,
        NULL,
        NULL,
        NULL,
        NULL
    FROM [dbo].[AccessRequest] AR
    WHERE AR.[OrganizationId] = @OrganizationId
        AND AR.[ExtensionOfLeaseId] IS NULL
        AND AR.[Status] = 3 -- Cancelled
        AND AR.[ResolvedDate] >= @Since

    UNION ALL
    -- RequestExpiredUnanswered: a pending request lapsed undecided (system; no actor).
    SELECT
        CAST(4 AS TINYINT),
        AR.[ResolvedDate],
        AR.[OrganizationId],
        NULL,
        AR.[RequesterId],
        AR.[CollectionId],
        AR.[CipherId],
        AR.[Id],
        NULL,
        NULL,
        NULL,
        NULL,
        NULL
    FROM [dbo].[AccessRequest] AR
    WHERE AR.[OrganizationId] = @OrganizationId
        AND AR.[ExtensionOfLeaseId] IS NULL
        AND AR.[Status] = 4 -- ExpiredUnanswered
        AND AR.[ResolvedDate] >= @Since

    UNION ALL
    -- RequestExpiredUnactivated (derived): an approved request whose window lapsed without ever minting a lease.
    SELECT
        CAST(5 AS TINYINT),
        AR.[NotAfter],
        AR.[OrganizationId],
        NULL,
        AR.[RequesterId],
        AR.[CollectionId],
        AR.[CipherId],
        AR.[Id],
        NULL,
        NULL,
        NULL,
        NULL,
        NULL
    FROM [dbo].[AccessRequest] AR
    WHERE AR.[OrganizationId] = @OrganizationId
        AND AR.[ExtensionOfLeaseId] IS NULL
        AND AR.[Status] = 1 -- Approved
        AND AR.[NotAfter] < @Now
        AND AR.[NotAfter] >= @Since
        AND NOT EXISTS (SELECT 1 FROM [dbo].[AccessLease] L WHERE L.[AccessRequestId] = AR.[Id])

    UNION ALL
    -- LeaseActivationRejected: a refused activation, recorded as AccessRequest.RejectedDate (last-only); actor = requester.
    SELECT
        CAST(11 AS TINYINT),
        AR.[RejectedDate],
        AR.[OrganizationId],
        AR.[RequesterId],
        AR.[RequesterId],
        AR.[CollectionId],
        AR.[CipherId],
        AR.[Id],
        NULL,
        NULL,
        NULL,
        NULL,
        NULL
    FROM [dbo].[AccessRequest] AR
    WHERE AR.[OrganizationId] = @OrganizationId
        AND AR.[ExtensionOfLeaseId] IS NULL
        AND AR.[RejectedDate] IS NOT NULL
        AND AR.[RejectedDate] >= @Since

    UNION ALL
    -- LeaseExtended: an (auto-approved) extension request pushed a parent lease's end out. AccessLeaseId = the parent
    -- lease; LeaseNotAfter = the new end.
    SELECT
        CAST(12 AS TINYINT),
        AR.[CreationDate],
        AR.[OrganizationId],
        AR.[RequesterId],
        AR.[RequesterId],
        AR.[CollectionId],
        AR.[CipherId],
        AR.[Id],
        AR.[ExtensionOfLeaseId],
        NULL,
        AR.[Reason],
        NULL,
        AR.[NotAfter]
    FROM [dbo].[AccessRequest] AR
    WHERE AR.[OrganizationId] = @OrganizationId
        AND AR.[ExtensionOfLeaseId] IS NOT NULL
        AND AR.[CreationDate] >= @Since

    UNION ALL
    -- LeaseActivated: the requester activated an approved request, minting the lease; actor = requester.
    SELECT
        CAST(10 AS TINYINT),
        L.[CreationDate],
        L.[OrganizationId],
        L.[RequesterId],
        L.[RequesterId],
        L.[CollectionId],
        L.[CipherId],
        L.[AccessRequestId],
        L.[Id],
        NULL,
        NULL,
        L.[NotBefore],
        L.[NotAfter]
    FROM [dbo].[AccessLease] L
    WHERE L.[OrganizationId] = @OrganizationId
        AND L.[CreationDate] >= @Since

    UNION ALL
    -- LeaseRevoked: a lease ended early by an operator (Revoked) or its own holder (Cancelled). Actor = RevokedBy.
    SELECT
        CAST(13 AS TINYINT),
        L.[RevokedDate],
        L.[OrganizationId],
        L.[RevokedBy],
        L.[RequesterId],
        L.[CollectionId],
        L.[CipherId],
        L.[AccessRequestId],
        L.[Id],
        NULL,
        RD.[Comment],
        L.[NotBefore],
        L.[NotAfter]
    FROM [dbo].[AccessLease] L
    OUTER APPLY (
        SELECT TOP 1 AD.[Comment]
        FROM [dbo].[AccessDecision] AD
        WHERE AD.[AccessRequestId] = L.[AccessRequestId]
            AND AD.[Verdict] = 0 -- Deny
            AND AD.[CreationDate] = L.[RevokedDate]
        ORDER BY AD.[CreationDate] DESC
    ) RD
    WHERE L.[OrganizationId] = @OrganizationId
        AND L.[Status] IN (2, 3) -- Revoked, Cancelled
        AND L.[RevokedDate] >= @Since

    UNION ALL
    -- LeaseExpired: a lease reached the end of its window (system; no actor). Occurs at NotAfter.
    SELECT
        CAST(14 AS TINYINT),
        L.[NotAfter],
        L.[OrganizationId],
        NULL,
        L.[RequesterId],
        L.[CollectionId],
        L.[CipherId],
        L.[AccessRequestId],
        L.[Id],
        NULL,
        NULL,
        L.[NotBefore],
        L.[NotAfter]
    FROM [dbo].[AccessLease] L
    WHERE L.[OrganizationId] = @OrganizationId
        AND (L.[Status] = 1 OR (L.[Status] = 0 AND L.[NotAfter] < @Now)) -- Expired, or Active and past its window
        AND L.[NotAfter] >= @Since

    UNION ALL
    -- RuleCreated: a governing rule was created; actor = its creator (LastEditedBy). Org-scoped, so a rule surfaces from
    -- creation whether or not it yet governs a collection.
    SELECT
        CAST(30 AS TINYINT),
        R.[CreationDate],
        R.[OrganizationId],
        R.[LastEditedBy],
        NULL,
        NULL,
        NULL,
        NULL,
        NULL,
        R.[Id],
        NULL,
        NULL,
        NULL
    FROM [dbo].[AccessRule] R
    WHERE R.[OrganizationId] = @OrganizationId
        AND R.[CreationDate] >= @Since

    UNION ALL
    -- RuleUpdated: the rule's latest edit (last-only), surfaced when RevisionDate moved past CreationDate.
    SELECT
        CAST(31 AS TINYINT),
        R.[RevisionDate],
        R.[OrganizationId],
        R.[LastEditedBy],
        NULL,
        NULL,
        NULL,
        NULL,
        NULL,
        R.[Id],
        NULL,
        NULL,
        NULL
    FROM [dbo].[AccessRule] R
    WHERE R.[OrganizationId] = @OrganizationId
        AND R.[RevisionDate] >= @Since
        AND R.[RevisionDate] > R.[CreationDate]

    UNION ALL
    -- RuleDeleted: a soft-deleted rule (DeletedDate set); actor = DeletedBy, occurring at DeletedDate.
    SELECT
        CAST(32 AS TINYINT),
        R.[DeletedDate],
        R.[OrganizationId],
        R.[DeletedBy],
        NULL,
        NULL,
        NULL,
        NULL,
        NULL,
        R.[Id],
        NULL,
        NULL,
        NULL
    FROM [dbo].[AccessRule] R
    WHERE R.[OrganizationId] = @OrganizationId
        AND R.[DeletedDate] IS NOT NULL
        AND R.[DeletedDate] >= @Since

    ) E
    LEFT JOIN [dbo].[User] AU ON AU.[Id] = E.[ActorId]
    LEFT JOIN [dbo].[User] RU ON RU.[Id] = E.[RequesterId]
    LEFT JOIN [dbo].[Cipher] C ON C.[Id] = E.[CipherId]
    LEFT JOIN [dbo].[Collection] COL ON COL.[Id] = E.[CollectionId]
    ORDER BY E.[OccurredAt] DESC
END
GO

-- The collection-scoped projection is superseded by the org-scoped one above.
DROP PROCEDURE IF EXISTS [dbo].[AccessAuditEvent_ReadManyByCollectionIds]
GO
