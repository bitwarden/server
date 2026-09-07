CREATE PROCEDURE [dbo].[AccessRequest_ReadDetailsById]
    @Id UNIQUEIDENTIFIER,
    @Now DATETIME2(7) = NULL
AS
BEGIN
    SET NOCOUNT ON

    -- @Now is accepted but unused now; only stored facts leave this read.
    -- Two result sets (request, decisions); only stored facts leave this read.
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
    LEFT JOIN [dbo].[User] U ON U.[Id] = LR.[RequesterId]
    LEFT JOIN [dbo].[AccessLease] PL ON PL.[AccessRequestId] = LR.[Id]
    WHERE LR.[Id] = @Id

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
    LEFT JOIN [dbo].[User] AU ON AU.[Id] = AD.[ApproverId]
    WHERE AD.[AccessRequestId] = @Id
    ORDER BY AD.[CreationDate] ASC
END
