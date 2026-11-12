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

    -- Every request that has ended, with how and when. Of the extensions, only a denied one has a row: an applied one
    -- moves its lease's end, and the client folds the rest away. Status values follow AccessRequestHistoryStatus.
    CREATE TABLE #Ended
    (
        [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        [CollectionId] UNIQUEIDENTIFIER NOT NULL,
        [RequesterId] UNIQUEIDENTIFIER NOT NULL,
        [Status] TINYINT NOT NULL,
        [EndDate] DATETIME2(7) NOT NULL
    )

    INSERT INTO #Ended ([Id], [CollectionId], [RequesterId], [Status], [EndDate])
    SELECT
        LR.[Id],
        LR.[CollectionId],
        LR.[RequesterId],
        S.[Status],
        CASE
            WHEN S.[Status] IN (0, 2) THEN COALESCE(LR.[ActionDate], LR.[CreationDate]) -- Denied, Canceled
            WHEN S.[Status] = 3 THEN LR.[NotAfter] -- Expired: unanswered, or approved and never started
            WHEN S.[Status] = 1 OR PL.[Action] = 3 THEN COALESCE(PL.[RevokedDate], PL.[NotAfter]) -- Revoked, or ended by its holder
            ELSE PL.[NotAfter] -- Ran to its end
        END
    FROM [dbo].[AccessRequest] LR
    LEFT JOIN [dbo].[AccessLease] PL ON PL.[AccessRequestId] = LR.[Id]
    CROSS APPLY (
        SELECT CASE
            WHEN LR.[ExtensionOfLeaseId] IS NOT NULL AND LR.[Action] <> 2 THEN NULL -- An extension not denied
            WHEN LR.[Action] = 2 THEN 0 -- Denied
            WHEN LR.[Action] = 3 THEN 2 -- Canceled
            WHEN LR.[Action] = 0 AND LR.[NotAfter] <= @Now THEN 3 -- Expired unanswered
            WHEN LR.[Action] <> 1 THEN NULL -- Open
            WHEN PL.[Id] IS NULL AND LR.[NotAfter] <= @Now THEN 3 -- Approved, never started
            WHEN PL.[Id] IS NULL THEN NULL -- Approved, not started yet
            WHEN PL.[Action] = 2 THEN 1 -- Revoked
            WHEN PL.[Action] = 3 OR PL.[NotAfter] <= @Now THEN 4 -- Session ended
        END AS [Status]
    ) S
    WHERE (
            LR.[CollectionId] IN (SELECT [Id] FROM @ManageableCollectionIds)
            OR LR.[RequesterId] = @UserId
        )
        AND LR.[CreationDate] >= @Since
        AND S.[Status] IS NOT NULL

    -- The filter options cover every ended row, so a filter never hides its own options.
    SELECT
        R.[RequesterId] AS [Id],
        U.[Name],
        U.[Email]
    FROM (SELECT DISTINCT [RequesterId] FROM #Ended) R
    LEFT JOIN [dbo].[User] U ON U.[Id] = R.[RequesterId]

    SELECT DISTINCT [CollectionId] FROM #Ended

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

    SELECT COUNT(*) FROM #Ended

    -- Two result sets (requests, decisions); materialized ids let both share the same rows.
    DECLARE @Page TABLE ([Id] UNIQUEIDENTIFIER PRIMARY KEY, [EndDate] DATETIME2(7) NOT NULL)

    INSERT INTO @Page ([Id], [EndDate])
    SELECT [Id], [EndDate]
    FROM #Ended
    ORDER BY [EndDate] DESC, [Id] DESC
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
    ORDER BY P.[EndDate] DESC, P.[Id] DESC

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
END
