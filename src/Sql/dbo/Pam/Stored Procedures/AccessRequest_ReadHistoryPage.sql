CREATE PROCEDURE [dbo].[AccessRequest_ReadHistoryPage]
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

    -- The filters, parsed once. Status values follow AccessRequestHistoryStatus.
    DECLARE @StatusFilter TABLE ([Value] TINYINT PRIMARY KEY)
    DECLARE @CollectionFilter TABLE ([Id] UNIQUEIDENTIFIER PRIMARY KEY)
    DECLARE @RequesterFilter TABLE ([Id] UNIQUEIDENTIFIER PRIMARY KEY)

    INSERT INTO @StatusFilter ([Value])
    SELECT DISTINCT CAST([value] AS TINYINT) FROM OPENJSON(@Statuses)

    INSERT INTO @CollectionFilter ([Id])
    SELECT DISTINCT CAST([value] AS UNIQUEIDENTIFIER) FROM OPENJSON(@CollectionIds)

    INSERT INTO @RequesterFilter ([Id])
    SELECT DISTINCT CAST([value] AS UNIQUEIDENTIFIER) FROM OPENJSON(@RequesterIds)

    -- Only Expired and Session ended need to know whether a request has a lease.
    DECLARE @NeedLease BIT = IIF(EXISTS (SELECT 1 FROM @StatusFilter WHERE [Value] IN (3, 4)), 1, 0)

    -- Revoked leases are few, so they are read from the lease side. A lease keeps its request's CollectionId and
    -- RequesterId.
    DECLARE @Revoked TABLE ([Id] UNIQUEIDENTIFIER PRIMARY KEY)

    IF @Statuses IS NOT NULL
    BEGIN
        INSERT INTO @Revoked ([Id])
        SELECT L.[AccessRequestId]
        FROM [dbo].[AccessLease] L
        WHERE L.[CollectionId] IN (SELECT [Id] FROM @ManageableCollectionIds)
            AND L.[Action] = 2 -- Revoked
        UNION
        SELECT L.[AccessRequestId]
        FROM [dbo].[AccessLease] L
        WHERE L.[RequesterId] = @UserId
            AND L.[Action] = 2 -- Revoked
    END

    -- Every request in scope that has ended, marked when it matches the filters. Of the extensions, only a denied one
    -- has a row: an applied one moves its lease's end, and the client folds the rest away.
    CREATE TABLE #Ended
    (
        [Id] UNIQUEIDENTIFIER NOT NULL,
        [CollectionId] UNIQUEIDENTIFIER NOT NULL,
        [RequesterId] UNIQUEIDENTIFIER NOT NULL,
        [CreationDate] DATETIME2(7) NOT NULL,
        [Match] BIT NOT NULL
    )

    -- RECOMPILE lets SQL drop the joins of the filters that are not set, and the lease join when no status needs it.
    INSERT INTO #Ended ([Id], [CollectionId], [RequesterId], [CreationDate], [Match])
    SELECT
        E.[Id],
        E.[CollectionId],
        E.[RequesterId],
        E.[CreationDate],
        IIF(
            (@CollectionIds IS NULL OR FC.[Id] IS NOT NULL)
            AND (@RequesterIds IS NULL OR FR.[Id] IS NOT NULL)
            AND (@Statuses IS NULL OR FS.[Value] IS NOT NULL),
            1, 0)
    FROM
    (
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
    ) E
    LEFT JOIN @CollectionFilter FC ON FC.[Id] = E.[CollectionId]
    LEFT JOIN @RequesterFilter FR ON FR.[Id] = E.[RequesterId]
    LEFT JOIN @Revoked RV ON RV.[Id] = E.[Id]
    LEFT JOIN [dbo].[AccessLease] PL ON @NeedLease = 1 AND PL.[AccessRequestId] = E.[Id]
    LEFT JOIN @StatusFilter FS ON FS.[Value] = CASE
        WHEN E.[Action] = 2 THEN 0 -- Denied
        WHEN E.[Action] = 3 THEN 2 -- Canceled
        WHEN RV.[Id] IS NOT NULL THEN 1 -- Revoked
        WHEN @NeedLease = 0 THEN NULL -- Expired or Session ended: not asked for
        WHEN PL.[AccessRequestId] IS NULL THEN 3 -- Expired: unanswered, or approved and never started
        ELSE 4 -- Session ended: ran to its end, or ended by its holder
    END
    OPTION (RECOMPILE)

    -- The filter options cover every ended row, so a filter never hides its own options.
    SELECT
        R.[RequesterId] AS [Id],
        U.[Name],
        U.[Email]
    FROM (SELECT DISTINCT [RequesterId] FROM #Ended) R
    LEFT JOIN [dbo].[User] U ON U.[Id] = R.[RequesterId]

    SELECT DISTINCT [CollectionId] FROM #Ended

    SELECT COUNT(*) FROM #Ended WHERE [Match] = 1

    -- Two result sets (requests, decisions); materialized ids let both share the same rows.
    DECLARE @Page TABLE ([Id] UNIQUEIDENTIFIER PRIMARY KEY)

    INSERT INTO @Page ([Id])
    SELECT [Id]
    FROM #Ended
    WHERE [Match] = 1
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
