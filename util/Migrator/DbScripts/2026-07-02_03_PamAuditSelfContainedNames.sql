-- Make each PAM audit event self-contained: snapshot the actor/requester/cipher/collection/rule display names into the
-- row at write time (resolved once by AccessAuditEvent_Create) so a later delete or rename of a referenced entity can no
-- longer erase or rewrite what the event said; the read proc then needs no joins. Cipher/collection names are encrypted
-- vault data (EncString) captured as-is -- a later org key-rotation may leave a historical snapshot undecryptable, but
-- the event and its ids still stand. Dapper/MSSQL only for this POC (no EF track).

IF COL_LENGTH('[dbo].[AccessAuditEvent]', 'ActorName') IS NULL
BEGIN
    ALTER TABLE [dbo].[AccessAuditEvent] ADD
        [ActorName]         NVARCHAR(50)    NULL,
        [ActorEmail]        NVARCHAR(256)   NULL,
        [RequesterName]     NVARCHAR(50)    NULL,
        [RequesterEmail]    NVARCHAR(256)   NULL,
        [CipherName]        NVARCHAR(MAX)   NULL,
        [CollectionName]    NVARCHAR(MAX)   NULL,
        [RuleName]          NVARCHAR(256)   NULL;
END
GO

CREATE OR ALTER PROCEDURE [dbo].[AccessAuditEvent_Create]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @Kind TINYINT,
    @Phase TINYINT,
    @OccurredAt DATETIME2(7),
    @ActorId UNIQUEIDENTIFIER = NULL,
    @RequesterId UNIQUEIDENTIFIER = NULL,
    @CollectionId UNIQUEIDENTIFIER = NULL,
    @CipherId UNIQUEIDENTIFIER = NULL,
    @AccessRequestId UNIQUEIDENTIFIER = NULL,
    @AccessLeaseId UNIQUEIDENTIFIER = NULL,
    @AccessRuleId UNIQUEIDENTIFIER = NULL,
    @Detail NVARCHAR(MAX) = NULL,
    @LeaseNotBefore DATETIME2(7) = NULL,
    @LeaseNotAfter DATETIME2(7) = NULL
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[AccessAuditEvent]
    (
        [Id],
        [OrganizationId],
        [Kind],
        [Phase],
        [OccurredAt],
        [ActorId],
        [RequesterId],
        [CollectionId],
        [CipherId],
        [AccessRequestId],
        [AccessLeaseId],
        [AccessRuleId],
        [Detail],
        [LeaseNotBefore],
        [LeaseNotAfter],
        [ActorName],
        [ActorEmail],
        [RequesterName],
        [RequesterEmail],
        [CipherName],
        [CollectionName],
        [RuleName]
    )
    SELECT
        @Id,
        @OrganizationId,
        @Kind,
        @Phase,
        @OccurredAt,
        @ActorId,
        @RequesterId,
        @CollectionId,
        @CipherId,
        @AccessRequestId,
        @AccessLeaseId,
        @AccessRuleId,
        @Detail,
        @LeaseNotBefore,
        @LeaseNotAfter,
        AU.[Name],
        AU.[Email],
        RU.[Name],
        RU.[Email],
        JSON_VALUE(C.[Data], '$.Name'),
        COL.[Name],
        RUL.[Name]
    FROM (SELECT 1 AS [X]) Seed
    LEFT JOIN [dbo].[User] AU ON AU.[Id] = @ActorId
    LEFT JOIN [dbo].[User] RU ON RU.[Id] = @RequesterId
    LEFT JOIN [dbo].[Cipher] C ON C.[Id] = @CipherId
    LEFT JOIN [dbo].[Collection] COL ON COL.[Id] = @CollectionId
    LEFT JOIN [dbo].[AccessRule] RUL ON RUL.[Id] = @AccessRuleId
END
GO

CREATE OR ALTER PROCEDURE [dbo].[AccessAuditEvent_ReadManyByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER,
    @Since DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        [Kind],
        [Phase],
        [OccurredAt],
        [OrganizationId],
        [ActorId],
        [RequesterId],
        [CollectionId],
        [CipherId],
        [AccessRequestId],
        [AccessLeaseId],
        [AccessRuleId],
        [Detail],
        [LeaseNotBefore],
        [LeaseNotAfter],
        [ActorName],
        [ActorEmail],
        [RequesterName],
        [RequesterEmail],
        [CipherName],
        [CollectionName],
        [RuleName]
    FROM [dbo].[AccessAuditEvent]
    WHERE [OrganizationId] = @OrganizationId
        AND [OccurredAt] >= @Since
    ORDER BY [OccurredAt] DESC
END
GO
