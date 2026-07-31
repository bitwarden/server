-- Switches the PAM audit-trail read from the previous read-time synthesis (a 14-branch UNION over PAM entity state) to
-- a plain read of the append-only [AccessAuditEvent] store added in the prior migration. Drops the @Now parameter: the
-- time-derived expiry kinds it computed are no longer synthesized -- they are deferred until an action (a sweep) writes
-- them. Existing history is not backfilled; the stored trail begins at deployment.

CREATE OR ALTER PROCEDURE [dbo].[AccessAuditEvent_ReadManyByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER,
    @Since DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        E.[Kind],
        E.[Phase],
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
        COL.[Name] AS [CollectionName],
        RUL.[Name] AS [RuleName]
    FROM [dbo].[AccessAuditEvent] E
    LEFT JOIN [dbo].[User] AU ON AU.[Id] = E.[ActorId]
    LEFT JOIN [dbo].[User] RU ON RU.[Id] = E.[RequesterId]
    LEFT JOIN [dbo].[Cipher] C ON C.[Id] = E.[CipherId]
    LEFT JOIN [dbo].[Collection] COL ON COL.[Id] = E.[CollectionId]
    LEFT JOIN [dbo].[AccessRule] RUL ON RUL.[Id] = E.[AccessRuleId]
    WHERE E.[OrganizationId] = @OrganizationId
        AND E.[OccurredAt] >= @Since
    ORDER BY E.[OccurredAt] DESC
END
GO
