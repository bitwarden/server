CREATE PROCEDURE [dbo].[AccessRequest_ReadInboxApprovedByCollectionIds]
    @CollectionIds [dbo].[GuidIdArray] READONLY,
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    -- Approved requests that can still give access: not started yet, or behind a live lease.
    DECLARE @RequestIds TABLE ([Id] UNIQUEIDENTIFIER PRIMARY KEY)

    INSERT INTO @RequestIds ([Id])
    SELECT LR.[Id]
    FROM [dbo].[AccessRequest] LR
    INNER JOIN @CollectionIds CI ON CI.[Id] = LR.[CollectionId]
    WHERE LR.[Action] = 1 -- Approved
        AND LR.[NotAfter] > @Now

    -- A lease ends at its request's NotAfter or, once extended, at its one extension's. So an extended lease that
    -- outlived its request's window is reached through the extension read above.
    INSERT INTO @RequestIds ([Id])
    SELECT DISTINCT L.[AccessRequestId]
    FROM @RequestIds RI
    INNER JOIN [dbo].[AccessRequest] X ON X.[Id] = RI.[Id]
    INNER JOIN [dbo].[AccessLease] L ON L.[Id] = X.[ExtensionOfLeaseId]
    WHERE L.[Action] = 0 -- None (no early end)
        AND L.[NotAfter] > @Now
        AND NOT EXISTS (SELECT 1 FROM @RequestIds E WHERE E.[Id] = L.[AccessRequestId])

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
        PL.[Id] AS [ProducedLeaseId],
        PL.[Action] AS [ProducedLeaseAction],
        PL.[NotAfter] AS [ProducedLeaseNotAfter],
        U.[Name] AS [RequesterName],
        U.[Email] AS [RequesterEmail]
    FROM [dbo].[AccessRequest] LR
    INNER JOIN @RequestIds RI ON RI.[Id] = LR.[Id]
    LEFT JOIN [dbo].[User] U ON U.[Id] = LR.[RequesterId]
    LEFT JOIN [dbo].[AccessLease] PL ON PL.[AccessRequestId] = LR.[Id]

    SELECT
        AD.[AccessRequestId],
        AD.[DeciderKind] AS [DeciderKind],
        AD.[ApproverId] AS [Id],
        AU.[Name] AS [Name],
        AU.[Email] AS [Email],
        AD.[Comment] AS [Comment],
        AD.[Verdict] AS [Verdict],
        AD.[CreationDate] AS [DecidedAt]
    FROM [dbo].[AccessDecision] AD
    INNER JOIN @RequestIds RI ON RI.[Id] = AD.[AccessRequestId]
    LEFT JOIN [dbo].[User] AU ON AU.[Id] = AD.[ApproverId]
    ORDER BY AD.[AccessRequestId], AD.[CreationDate] ASC
END
