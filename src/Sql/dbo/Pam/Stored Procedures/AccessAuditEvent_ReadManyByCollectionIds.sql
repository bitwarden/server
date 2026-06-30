CREATE PROCEDURE [dbo].[AccessAuditEvent_ReadManyByCollectionIds]
    @CollectionIds [dbo].[GuidIdArray] READONLY,
    @Since DATETIME2(7),
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    -- Synthesizes the PAM access-audit trail for the supplied (caller-manageable) collections: every event occurring on
    -- or after @Since, newest first. There is no audit table -- each row is projected from the lifecycle state the PAM
    -- entities already retain. Covers the access-request, access-lease, and rule administration kinds; the rule kinds
    -- scope through Collection.AccessRuleId (the link survives an AccessRule soft-delete, so rule_deleted scopes too).
    -- Kind codes match Bit.Pam.Enums.AccessAuditEventKind.

    -- Project the events from existing PAM state (the inner query), then join once to denormalize display names: actor
    -- and requester from [User] (plaintext); cipher/collection names (encrypted, decrypted client-side) from
    -- [Cipher]/[Collection].
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
    INNER JOIN @CollectionIds CI ON CI.[Id] = AR.[CollectionId]
    WHERE AR.[ExtensionOfLeaseId] IS NULL
        AND AR.[CreationDate] >= @Since

    UNION ALL
    -- RequestApproved: every approval (human or automatic) is recorded as an AccessDecision with Verdict = Approve.
    -- Actor = the approver, or NULL for an automatic decision. Detail = the approver comment, if any.
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
    INNER JOIN @CollectionIds CI ON CI.[Id] = AR.[CollectionId]
    WHERE AR.[ExtensionOfLeaseId] IS NULL          -- extension approvals surface as LeaseExtended instead
        AND AD.[Verdict] = 1 -- Approve
        AND AD.[CreationDate] >= @Since

    UNION ALL
    -- RequestDenied: a Deny decision on a request that ended Denied. A Deny decision on a still-Approved request is a
    -- lease revoke (its reason is stored as a decision); that surfaces as LeaseRevoked from the lease, not here.
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
    INNER JOIN @CollectionIds CI ON CI.[Id] = AR.[CollectionId]
    WHERE AR.[ExtensionOfLeaseId] IS NULL
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
    INNER JOIN @CollectionIds CI ON CI.[Id] = AR.[CollectionId]
    WHERE AR.[ExtensionOfLeaseId] IS NULL
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
    INNER JOIN @CollectionIds CI ON CI.[Id] = AR.[CollectionId]
    WHERE AR.[ExtensionOfLeaseId] IS NULL
        AND AR.[Status] = 4 -- ExpiredUnanswered
        AND AR.[ResolvedDate] >= @Since

    UNION ALL
    -- RequestExpiredUnactivated (derived): an approved request whose window lapsed without ever minting a lease.
    -- Occurs at NotAfter; no row changed, so it is computed against @Now (system; no actor).
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
    INNER JOIN @CollectionIds CI ON CI.[Id] = AR.[CollectionId]
    WHERE AR.[ExtensionOfLeaseId] IS NULL
        AND AR.[Status] = 1 -- Approved
        AND AR.[NotAfter] < @Now
        AND AR.[NotAfter] >= @Since
        AND NOT EXISTS (SELECT 1 FROM [dbo].[AccessLease] L WHERE L.[AccessRequestId] = AR.[Id])

    UNION ALL
    -- LeaseActivationRejected: a refused activation, recorded as AccessRequest.RejectedDate (last-only). The request
    -- stays Approved and re-activatable; actor = requester.
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
    INNER JOIN @CollectionIds CI ON CI.[Id] = AR.[CollectionId]
    WHERE AR.[ExtensionOfLeaseId] IS NULL
        AND AR.[RejectedDate] IS NOT NULL
        AND AR.[RejectedDate] >= @Since

    UNION ALL
    -- LeaseExtended: an (auto-approved) extension request pushed a parent lease's end out. Surfaced from the extension
    -- request rather than as a second submit/approve pair. AccessLeaseId = the parent lease; LeaseNotAfter = the new end.
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
    INNER JOIN @CollectionIds CI ON CI.[Id] = AR.[CollectionId]
    WHERE AR.[ExtensionOfLeaseId] IS NOT NULL
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
    INNER JOIN @CollectionIds CI ON CI.[Id] = L.[CollectionId]
    WHERE L.[CreationDate] >= @Since

    UNION ALL
    -- LeaseRevoked: a lease ended early -- by an operator (Revoked) or its own holder (Cancelled). Actor = RevokedBy
    -- (= requester for a self-end). Detail = the reason, stored as the Deny decision written when the lease ended.
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
    INNER JOIN @CollectionIds CI ON CI.[Id] = L.[CollectionId]
    OUTER APPLY (
        SELECT TOP 1 AD.[Comment]
        FROM [dbo].[AccessDecision] AD
        WHERE AD.[AccessRequestId] = L.[AccessRequestId]
            AND AD.[Verdict] = 0 -- Deny
            AND AD.[CreationDate] = L.[RevokedDate]
        ORDER BY AD.[CreationDate] DESC
    ) RD
    WHERE L.[Status] IN (2, 3) -- Revoked, Cancelled
        AND L.[RevokedDate] >= @Since

    UNION ALL
    -- LeaseExpired: a lease reached the end of its window (system; no actor). Covers leases a sweep has marked Expired
    -- and still-Active leases already past NotAfter (computed against @Now). Occurs at NotAfter.
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
    INNER JOIN @CollectionIds CI ON CI.[Id] = L.[CollectionId]
    WHERE (L.[Status] = 1 OR (L.[Status] = 0 AND L.[NotAfter] < @Now)) -- Expired, or Active and past its window
        AND L.[NotAfter] >= @Since

    UNION ALL
    -- RuleCreated: a governing rule was created; actor = its creator (LastEditedBy). Scoped to rules governing a
    -- caller-manageable collection. CollectionId is NULL -- a rule may span collections; the event is the rule's.
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
    WHERE R.[CreationDate] >= @Since
        AND EXISTS (
            SELECT 1 FROM [dbo].[Collection] GC
            INNER JOIN @CollectionIds CI ON CI.[Id] = GC.[Id]
            WHERE GC.[AccessRuleId] = R.[Id])

    UNION ALL
    -- RuleUpdated: the rule's latest edit (last-only), surfaced when RevisionDate moved past CreationDate; actor =
    -- LastEditedBy. Same collection scoping as RuleCreated.
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
    WHERE R.[RevisionDate] >= @Since
        AND R.[RevisionDate] > R.[CreationDate]
        AND EXISTS (
            SELECT 1 FROM [dbo].[Collection] GC
            INNER JOIN @CollectionIds CI ON CI.[Id] = GC.[Id]
            WHERE GC.[AccessRuleId] = R.[Id])

    UNION ALL
    -- RuleDeleted: a soft-deleted rule (DeletedDate set); actor = DeletedBy, occurring at DeletedDate. The rule's
    -- Collection.AccessRuleId links are preserved on delete, so this scopes the same way as RuleCreated/Updated.
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
    WHERE R.[DeletedDate] IS NOT NULL
        AND R.[DeletedDate] >= @Since
        AND EXISTS (
            SELECT 1 FROM [dbo].[Collection] GC
            INNER JOIN @CollectionIds CI ON CI.[Id] = GC.[Id]
            WHERE GC.[AccessRuleId] = R.[Id])

    ) E
    LEFT JOIN [dbo].[User] AU ON AU.[Id] = E.[ActorId]
    LEFT JOIN [dbo].[User] RU ON RU.[Id] = E.[RequesterId]
    LEFT JOIN [dbo].[Cipher] C ON C.[Id] = E.[CipherId]
    LEFT JOIN [dbo].[Collection] COL ON COL.[Id] = E.[CollectionId]
    ORDER BY E.[OccurredAt] DESC
END
