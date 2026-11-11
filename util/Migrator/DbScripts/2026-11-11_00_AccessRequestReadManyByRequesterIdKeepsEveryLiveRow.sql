-- The requester's list returns every live request; only the rest stays capped at 250.

CREATE OR ALTER PROCEDURE [dbo].[AccessRequest_ReadManyByRequesterId]
    @RequesterId UNIQUEIDENTIFIER,
    @Now DATETIME2(7),
    @Since DATETIME2(7) = NULL
AS
BEGIN
    SET NOCOUNT ON

    -- @Since matches the approver-side retention window.
    -- Ids are materialized first so both result sets share the same rows.
    DECLARE @RequestIds TABLE ([Id] UNIQUEIDENTIFIER PRIMARY KEY)

    -- Every live row, uncapped: the vault badges and the nav count read them from this list.
    INSERT INTO @RequestIds ([Id])
    SELECT AR.[Id]
    FROM [dbo].[AccessRequest] AR
    WHERE AR.[RequesterId] = @RequesterId
        AND (
            (AR.[Action] IN (0, 1) AND AR.[NotAfter] > @Now) -- open and answerable, or approved and activatable
            OR EXISTS ( -- produced a lease still running, which an extension can keep past the request's window
                SELECT 1
                FROM [dbo].[AccessLease] AL
                WHERE AL.[AccessRequestId] = AR.[Id]
                    AND AL.[Action] = 0 -- None (no early end)
                    AND AL.[NotAfter] > @Now
            )
        )

    -- Then the newest of the rest, capped.
    INSERT INTO @RequestIds ([Id])
    SELECT TOP (250) AR.[Id]
    FROM [dbo].[AccessRequest] AR
    WHERE AR.[RequesterId] = @RequesterId
        AND (@Since IS NULL OR AR.[CreationDate] >= @Since)
        AND NOT EXISTS (SELECT 1 FROM @RequestIds RI WHERE RI.[Id] = AR.[Id])
    ORDER BY AR.[CreationDate] DESC

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
        LR.[Action],
        LR.[CreationDate],
        LR.[ActionDate],
        LR.[RuleId],
        PL.[Id] AS [ProducedLeaseId],
        PL.[Action] AS [ProducedLeaseAction],
        PL.[NotAfter] AS [ProducedLeaseNotAfter]
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
GO
