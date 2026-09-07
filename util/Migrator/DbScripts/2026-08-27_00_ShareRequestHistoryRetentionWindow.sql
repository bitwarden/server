-- Holds the requester's own history to the same retention window as the approver.
-- Live rows (Pending, or Approved unlapsed) are exempt from windowing.
CREATE OR ALTER PROCEDURE [dbo].[AccessRequest_ReadManyByRequesterId]
    @RequesterId UNIQUEIDENTIFIER,
    @Now DATETIME2(7) = NULL,
    @Since DATETIME2(7) = NULL
AS
BEGIN
    SET NOCOUNT ON

    -- Lets older callers omit @Now and @Since during rolling deployment.
    SET @Now = COALESCE(@Now, GETUTCDATE())

    -- @Since matches the approver-side retention window.
    -- Ids are materialized first so both result sets share the same rows.
    DECLARE @RequestIds TABLE ([Id] UNIQUEIDENTIFIER PRIMARY KEY)

    INSERT INTO @RequestIds ([Id])
    SELECT TOP (250) [Id]
    FROM [dbo].[AccessRequest]
    WHERE [RequesterId] = @RequesterId
        AND (
            @Since IS NULL
            OR [CreationDate] >= @Since
            OR [Status] = 0 -- Pending: awaiting a decision
            OR ([Status] = 1 AND [NotAfter] > @Now) -- Approved with an unlapsed window
        )
    ORDER BY [CreationDate] DESC

    -- A request produces at most one lease, so this joins at most one row.
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
        LR.[Status],
        LR.[CreationDate],
        LR.[ResolvedDate],
        LR.[RuleId],
        PL.[Id] AS [ProducedLeaseId],
        -- Expired is never stored; derive it against @Now off the lease's own NotAfter.
        CASE WHEN PL.[Status] = 0 AND PL.[NotAfter] <= @Now THEN 1 ELSE PL.[Status] END AS [ProducedLeaseStatus]
    FROM [dbo].[AccessRequest] LR
    INNER JOIN @RequestIds RI ON RI.[Id] = LR.[Id]
    LEFT JOIN [dbo].[AccessLease] PL ON PL.[AccessRequestId] = LR.[Id]
    ORDER BY LR.[CreationDate] DESC

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
