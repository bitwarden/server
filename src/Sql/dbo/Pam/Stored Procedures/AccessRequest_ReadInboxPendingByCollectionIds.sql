CREATE PROCEDURE [dbo].[AccessRequest_ReadInboxPendingByCollectionIds]
    @CollectionIds [dbo].[GuidIdArray] READONLY,
    @Now DATETIME2(7) = NULL
AS
BEGIN
    SET NOCOUNT ON

    -- Lets older callers omit @Now during rolling deployment.
    SET @Now = COALESCE(@Now, GETUTCDATE())

    -- Actionable rows have no action and an open window; lapsed rows derive Expired instead.
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
        U.[Name] AS [RequesterName],
        U.[Email] AS [RequesterEmail]
    FROM [dbo].[AccessRequest] LR
    INNER JOIN @CollectionIds CI ON CI.[Id] = LR.[CollectionId]
    LEFT JOIN [dbo].[User] U ON U.[Id] = LR.[RequesterId]
    WHERE LR.[Action] = 0 -- None (open)
        AND LR.[NotAfter] > @Now
END
