
-- PAM Approver Inbox: read procedures for the pending/history queues, the resolve-with-decision and lease-revoke
-- mutations, and Collection_ReadManagingUserIds (the collection managers resolved for the RefreshApproverInbox push).

CREATE OR ALTER PROCEDURE [dbo].[AccessRequest_ReadInboxPendingByCollectionIds]
    @CollectionIds [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

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
        PL.[Id] AS [ProducedLeaseId],
        RES.[ApproverId] AS [ApproverId],
        RES.[Comment] AS [ApproverComment],
        JSON_VALUE(C.[Data], '$.Name') AS [CipherName],
        COL.[Name] AS [CollectionName],
        U.[Name] AS [RequesterName],
        U.[Email] AS [RequesterEmail]
    FROM [dbo].[AccessRequest] LR
    INNER JOIN @CollectionIds CI ON CI.[Id] = LR.[CollectionId]
    LEFT JOIN [dbo].[Cipher] C ON C.[Id] = LR.[CipherId]
    LEFT JOIN [dbo].[Collection] COL ON COL.[Id] = LR.[CollectionId]
    LEFT JOIN [dbo].[User] U ON U.[Id] = LR.[RequesterId]
    OUTER APPLY (
        SELECT TOP 1 L.[Id]
        FROM [dbo].[AccessLease] L
        WHERE L.[AccessRequestId] = LR.[Id]
        ORDER BY L.[CreationDate] DESC
    ) PL
    OUTER APPLY (
        SELECT TOP 1 LD.[ApproverId], LD.[Comment]
        FROM [dbo].[AccessDecision] LD
        WHERE LD.[AccessRequestId] = LR.[Id] AND LD.[DeciderKind] = 1 -- Human
        ORDER BY LD.[CreationDate] ASC
    ) RES
    WHERE LR.[Status] = 0 -- Pending
END
GO

CREATE OR ALTER PROCEDURE [dbo].[AccessRequest_ReadInboxHistoryByCollectionIds]
    @CollectionIds [dbo].[GuidIdArray] READONLY,
    @Since DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

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
        PL.[Id] AS [ProducedLeaseId],
        RES.[ApproverId] AS [ApproverId],
        RES.[Comment] AS [ApproverComment],
        JSON_VALUE(C.[Data], '$.Name') AS [CipherName],
        COL.[Name] AS [CollectionName],
        U.[Name] AS [RequesterName],
        U.[Email] AS [RequesterEmail]
    FROM [dbo].[AccessRequest] LR
    INNER JOIN @CollectionIds CI ON CI.[Id] = LR.[CollectionId]
    LEFT JOIN [dbo].[Cipher] C ON C.[Id] = LR.[CipherId]
    LEFT JOIN [dbo].[Collection] COL ON COL.[Id] = LR.[CollectionId]
    LEFT JOIN [dbo].[User] U ON U.[Id] = LR.[RequesterId]
    OUTER APPLY (
        SELECT TOP 1 L.[Id]
        FROM [dbo].[AccessLease] L
        WHERE L.[AccessRequestId] = LR.[Id]
        ORDER BY L.[CreationDate] DESC
    ) PL
    OUTER APPLY (
        SELECT TOP 1 LD.[ApproverId], LD.[Comment]
        FROM [dbo].[AccessDecision] LD
        WHERE LD.[AccessRequestId] = LR.[Id] AND LD.[DeciderKind] = 1 -- Human
        ORDER BY LD.[CreationDate] ASC
    ) RES
    WHERE LR.[Status] <> 0 -- not Pending
        AND LR.[CreationDate] >= @Since
END
GO

CREATE OR ALTER PROCEDURE [dbo].[AccessRequest_ResolveWithDecision]
    @AccessRequestId UNIQUEIDENTIFIER,
    @Status TINYINT,
    @AccessDecisionId UNIQUEIDENTIFIER,
    @ApproverId UNIQUEIDENTIFIER,
    @Verdict TINYINT,
    @Comment NVARCHAR(MAX) = NULL,
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    BEGIN TRANSACTION AccessRequest_Resolve

    UPDATE [dbo].[AccessRequest]
    SET [Status] = @Status,
        [ResolvedDate] = @Now
    WHERE [Id] = @AccessRequestId AND [Status] = 0 -- Pending

    INSERT INTO [dbo].[AccessDecision]
    (
        [Id], [AccessRequestId], [DeciderKind], [ApproverId], [ConditionKind],
        [Verdict], [Comment], [EvaluationContext], [CreationDate]
    )
    VALUES
    (
        @AccessDecisionId, @AccessRequestId, 1 /* Human */, @ApproverId, NULL,
        @Verdict, @Comment, NULL, @Now
    )

    COMMIT TRANSACTION AccessRequest_Resolve
END
GO

CREATE OR ALTER PROCEDURE [dbo].[AccessLease_Revoke]
    @AccessLeaseId UNIQUEIDENTIFIER,
    @AccessRequestId UNIQUEIDENTIFIER,
    @RevokedBy UNIQUEIDENTIFIER,
    @AccessDecisionId UNIQUEIDENTIFIER,
    @Reason NVARCHAR(MAX) = NULL,
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    BEGIN TRANSACTION AccessLease_Revoke

    UPDATE [dbo].[AccessLease]
    SET [Status] = 2 /* Revoked */,
        [RevokedDate] = @Now,
        [RevokedBy] = @RevokedBy
    WHERE [Id] = @AccessLeaseId AND [Status] = 0 -- Active

    INSERT INTO [dbo].[AccessDecision]
    (
        [Id], [AccessRequestId], [DeciderKind], [ApproverId], [ConditionKind],
        [Verdict], [Comment], [EvaluationContext], [CreationDate]
    )
    VALUES
    (
        @AccessDecisionId, @AccessRequestId, 1 /* Human */, @RevokedBy, NULL,
        1 /* Deny */, @Reason, NULL, @Now
    )

    COMMIT TRANSACTION AccessLease_Revoke
END
GO

CREATE OR ALTER PROCEDURE [dbo].[Collection_ReadManagingUserIds]
    @CollectionId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @OrganizationId UNIQUEIDENTIFIER
    SELECT @OrganizationId = [OrganizationId] FROM [dbo].[Collection] WHERE [Id] = @CollectionId

    SELECT DISTINCT [UserId]
    FROM
    (
        SELECT OU.[UserId]
        FROM [dbo].[CollectionUser] CU
        INNER JOIN [dbo].[OrganizationUser] OU ON OU.[Id] = CU.[OrganizationUserId]
        WHERE CU.[CollectionId] = @CollectionId
            AND CU.[Manage] = 1
            AND OU.[Status] = 2 -- Confirmed
            AND OU.[UserId] IS NOT NULL

        UNION

        SELECT OU.[UserId]
        FROM [dbo].[CollectionGroup] CG
        INNER JOIN [dbo].[GroupUser] GU ON GU.[GroupId] = CG.[GroupId]
        INNER JOIN [dbo].[OrganizationUser] OU ON OU.[Id] = GU.[OrganizationUserId]
        WHERE CG.[CollectionId] = @CollectionId
            AND CG.[Manage] = 1
            AND OU.[Status] = 2 -- Confirmed
            AND OU.[UserId] IS NOT NULL

        UNION

        SELECT OU.[UserId]
        FROM [dbo].[OrganizationUser] OU
        INNER JOIN [dbo].[Organization] O ON O.[Id] = OU.[OrganizationId]
        WHERE OU.[OrganizationId] = @OrganizationId
            AND OU.[Status] = 2 -- Confirmed
            AND OU.[UserId] IS NOT NULL
            AND (
                (O.[AllowAdminAccessToAllCollectionItems] = 1 AND OU.[Type] IN (0, 1)) -- Owner, Admin
                OR (OU.[Type] = 4 -- Custom
                    AND ISJSON(OU.[Permissions]) = 1
                    AND JSON_VALUE(OU.[Permissions], '$.editAnyCollection') = 'true')
            )
    ) AS ManagingUsers
END
GO
