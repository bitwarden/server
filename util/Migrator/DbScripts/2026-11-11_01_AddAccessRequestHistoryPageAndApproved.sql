-- Adds the paged approver history read and the read of approved requests that can still give access, so the
-- Approvals tab no longer reads the whole history. AccessRequest_ReadInboxHistoryByCollectionIds stays for servers
-- that still call it during the deploy.

-- The history page reads its rows from this index alone, without a lookup per row.
IF EXISTS (
    SELECT
        NULL
    FROM
        sys.indexes
    WHERE
        [name] = 'IX_AccessRequest_CollectionId_CreationDate'
        AND object_id = OBJECT_ID('[dbo].[AccessRequest]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AccessRequest_CollectionId_CreationDate]
    ON [dbo].[AccessRequest] ([CollectionId] ASC, [CreationDate] ASC)
    INCLUDE ([Action], [NotAfter], [RequesterId], [ExtensionOfLeaseId])
    WITH (DROP_EXISTING = ON);
END
ELSE
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AccessRequest_CollectionId_CreationDate]
    ON [dbo].[AccessRequest] ([CollectionId] ASC, [CreationDate] ASC)
    INCLUDE ([Action], [NotAfter], [RequesterId], [ExtensionOfLeaseId]);
END
GO

CREATE OR ALTER PROCEDURE [dbo].[AccessRequest_ReadHistoryPage]
    -- The scope: requests on the collections the caller can Manage, and the caller's own wherever they are.
    @ManageableCollectionIds [dbo].[GuidIdArray] READONLY,
    @UserId UNIQUEIDENTIFIER,
    @Since DATETIME2(7),
    @Now DATETIME2(7),
    @Skip INT,
    @Take INT,
    -- JSON arrays ([0,3], ["<guid>",...]); NULL leaves the dimension unfiltered.
    @Statuses NVARCHAR(MAX) = NULL,
    @CollectionIds NVARCHAR(MAX) = NULL,
    @RequesterIds NVARCHAR(MAX) = NULL
AS
BEGIN
    SET NOCOUNT ON

    -- The requests in scope that have not ended: open, approved and not started, or behind a live lease. They are few,
    -- so the history is the rest of the scope and no row needs its lease.
    CREATE TABLE #Live ([Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY)

    INSERT INTO #Live ([Id])
    SELECT LR.[Id]
    FROM [dbo].[AccessRequest] LR
    WHERE LR.[CollectionId] IN (SELECT [Id] FROM @ManageableCollectionIds)
        AND LR.[Action] IN (0, 1) -- None (open), Approved
        AND LR.[NotAfter] > @Now
    UNION
    SELECT LR.[Id]
    FROM [dbo].[AccessRequest] LR
    WHERE LR.[RequesterId] = @UserId
        AND LR.[Action] IN (0, 1) -- None (open), Approved
        AND LR.[NotAfter] > @Now

    -- A lease ends at its request's NotAfter or, once extended, at its one extension's. So a lease that outlived its
    -- request's window is reached through the extension read above.
    INSERT INTO #Live ([Id])
    SELECT DISTINCT L.[AccessRequestId]
    FROM #Live LV
    INNER JOIN [dbo].[AccessRequest] X ON X.[Id] = LV.[Id]
    INNER JOIN [dbo].[AccessLease] L ON L.[Id] = X.[ExtensionOfLeaseId]
    WHERE X.[Action] = 1 -- Approved
        AND L.[Action] = 0 -- None (no early end)
        AND NOT EXISTS (SELECT 1 FROM #Live E WHERE E.[Id] = L.[AccessRequestId])

    -- A lease revoked or ended by its holder ends its request early.
    DELETE LV
    FROM #Live LV
    INNER JOIN [dbo].[AccessLease] L ON L.[AccessRequestId] = LV.[Id]
    WHERE L.[Action] <> 0 -- None (no early end)

    -- Every request in scope that has ended. Of the extensions, only a denied one has a row: an applied one moves its
    -- lease's end, and the client folds the rest away.
    CREATE TABLE #Ended
    (
        [Id] UNIQUEIDENTIFIER NOT NULL,
        [CollectionId] UNIQUEIDENTIFIER NOT NULL,
        [RequesterId] UNIQUEIDENTIFIER NOT NULL,
        [CreationDate] DATETIME2(7) NOT NULL,
        [Action] TINYINT NOT NULL,
        [Status] TINYINT NULL
    )

    INSERT INTO #Ended ([Id], [CollectionId], [RequesterId], [CreationDate], [Action])
    SELECT LR.[Id], LR.[CollectionId], LR.[RequesterId], LR.[CreationDate], LR.[Action]
    FROM [dbo].[AccessRequest] LR
    WHERE LR.[CollectionId] IN (SELECT [Id] FROM @ManageableCollectionIds)
        AND LR.[CreationDate] >= @Since
        AND (LR.[ExtensionOfLeaseId] IS NULL OR LR.[Action] = 2) -- Denied
        AND NOT EXISTS (SELECT 1 FROM #Live LV WHERE LV.[Id] = LR.[Id])
    UNION ALL
    SELECT LR.[Id], LR.[CollectionId], LR.[RequesterId], LR.[CreationDate], LR.[Action]
    FROM [dbo].[AccessRequest] LR
    WHERE LR.[RequesterId] = @UserId
        AND LR.[CollectionId] NOT IN (SELECT [Id] FROM @ManageableCollectionIds)
        AND LR.[CreationDate] >= @Since
        AND (LR.[ExtensionOfLeaseId] IS NULL OR LR.[Action] = 2) -- Denied
        AND NOT EXISTS (SELECT 1 FROM #Live LV WHERE LV.[Id] = LR.[Id])

    -- The filter options cover every ended row, so a filter never hides its own options.
    SELECT
        R.[RequesterId] AS [Id],
        U.[Name],
        U.[Email]
    FROM (SELECT DISTINCT [RequesterId] FROM #Ended) R
    LEFT JOIN [dbo].[User] U ON U.[Id] = R.[RequesterId]

    SELECT DISTINCT [CollectionId] FROM #Ended

    -- The status needs the lease, so it is read only for a status filter. Values follow AccessRequestHistoryStatus.
    IF @Statuses IS NOT NULL
    BEGIN
        UPDATE E
        SET [Status] = CASE
            WHEN E.[Action] = 2 THEN 0 -- Denied
            WHEN E.[Action] = 3 THEN 2 -- Canceled
            WHEN PL.[Id] IS NULL THEN 3 -- Expired: unanswered, or approved and never started
            WHEN PL.[Action] = 2 THEN 1 -- Revoked
            ELSE 4 -- Session ended: ran to its end, or ended by its holder
        END
        FROM #Ended E
        LEFT JOIN [dbo].[AccessLease] PL ON PL.[AccessRequestId] = E.[Id]
    END

    IF @Statuses IS NOT NULL OR @CollectionIds IS NOT NULL OR @RequesterIds IS NOT NULL
    BEGIN
        DELETE FROM #Ended
        WHERE NOT (
            (@Statuses IS NULL OR [Status] IN (SELECT CAST([value] AS TINYINT) FROM OPENJSON(@Statuses)))
            AND (
                @CollectionIds IS NULL
                OR [CollectionId] IN (SELECT CAST([value] AS UNIQUEIDENTIFIER) FROM OPENJSON(@CollectionIds))
            )
            AND (
                @RequesterIds IS NULL
                OR [RequesterId] IN (SELECT CAST([value] AS UNIQUEIDENTIFIER) FROM OPENJSON(@RequesterIds))
            )
        )
    END

    SELECT COUNT(*) FROM #Ended

    -- Two result sets (requests, decisions); materialized ids let both share the same rows.
    DECLARE @Page TABLE ([Id] UNIQUEIDENTIFIER PRIMARY KEY)

    INSERT INTO @Page ([Id])
    SELECT [Id]
    FROM #Ended
    ORDER BY [CreationDate] DESC, [Id] DESC
    OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY

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
    FROM @Page P
    INNER JOIN [dbo].[AccessRequest] LR ON LR.[Id] = P.[Id]
    LEFT JOIN [dbo].[User] U ON U.[Id] = LR.[RequesterId]
    LEFT JOIN [dbo].[AccessLease] PL ON PL.[AccessRequestId] = LR.[Id]
    ORDER BY LR.[CreationDate] DESC, LR.[Id] DESC

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
    INNER JOIN @Page P ON P.[Id] = AD.[AccessRequestId]
    LEFT JOIN [dbo].[User] AU ON AU.[Id] = AD.[ApproverId]
    ORDER BY AD.[AccessRequestId], AD.[CreationDate] ASC

    DROP TABLE #Ended
    DROP TABLE #Live
END
GO

CREATE OR ALTER PROCEDURE [dbo].[AccessRequest_ReadInboxApprovedByCollectionIds]
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
GO
