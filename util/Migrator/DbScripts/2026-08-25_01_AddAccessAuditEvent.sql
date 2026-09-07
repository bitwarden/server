-- Adds the PAM access-audit log: append-only, names snapshotted at write.
-- Only OrganizationId is foreign keyed among the subject ids.
IF OBJECT_ID('[dbo].[AccessAuditEvent]') IS NULL
BEGIN
    CREATE TABLE [dbo].[AccessAuditEvent] (
        [Id]                UNIQUEIDENTIFIER    NOT NULL,
        [OrganizationId]    UNIQUEIDENTIFIER    NOT NULL,
        [Kind]              TINYINT             NOT NULL,
        [Phase]             TINYINT             NOT NULL,
        [OccurredAt]        DATETIME2(7)        NOT NULL,
        [ActorId]           UNIQUEIDENTIFIER    NULL,
        [RequesterId]       UNIQUEIDENTIFIER    NULL,
        [CollectionId]      UNIQUEIDENTIFIER    NULL,
        [CipherId]          UNIQUEIDENTIFIER    NULL,
        [AccessRequestId]   UNIQUEIDENTIFIER    NULL,
        [AccessLeaseId]     UNIQUEIDENTIFIER    NULL,
        [AccessRuleId]      UNIQUEIDENTIFIER    NULL,
        [Detail]            NVARCHAR(MAX)       NULL,
        [LeaseNotBefore]    DATETIME2(7)        NULL,
        [LeaseNotAfter]     DATETIME2(7)        NULL,
        [ActorName]         NVARCHAR(50)        NULL,
        [ActorEmail]        NVARCHAR(256)       NULL,
        [RequesterName]     NVARCHAR(50)        NULL,
        [RequesterEmail]    NVARCHAR(256)       NULL,
        [CipherName]        NVARCHAR(MAX)       NULL,
        [CollectionName]    NVARCHAR(MAX)       NULL,
        [RuleName]          NVARCHAR(256)       NULL,
        [CorrelationId]     UNIQUEIDENTIFIER    NOT NULL CONSTRAINT [DF_AccessAuditEvent_CorrelationId] DEFAULT NEWID(),
        CONSTRAINT [PK_AccessAuditEvent] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_AccessAuditEvent_Organization] FOREIGN KEY ([OrganizationId])
            REFERENCES [dbo].[Organization] ([Id]) ON DELETE CASCADE
    );
END
GO

-- Serves the only read; DESC key order lets ORDER BY come off the index.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'IX_AccessAuditEvent_OrganizationId_OccurredAt' AND object_id = OBJECT_ID('[dbo].[AccessAuditEvent]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AccessAuditEvent_OrganizationId_OccurredAt]
        ON [dbo].[AccessAuditEvent] ([OrganizationId] ASC, [OccurredAt] DESC);
END
GO

CREATE OR ALTER PROCEDURE [dbo].[AccessAuditEvent_Create]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @CorrelationId UNIQUEIDENTIFIER,
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
    @RuleName NVARCHAR(256) = NULL,
    @Detail NVARCHAR(MAX) = NULL,
    @LeaseNotBefore DATETIME2(7) = NULL,
    @LeaseNotAfter DATETIME2(7) = NULL
AS
BEGIN
    SET NOCOUNT ON

    -- Snapshots names at write; later deletes/renames can't change the event.
    -- @RuleName is caller-supplied, not JOINed, since rules can be hard-deleted.
    INSERT INTO [dbo].[AccessAuditEvent]
    (
        [Id],
        [OrganizationId],
        [CorrelationId],
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
        @CorrelationId,
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
        @RuleName
    FROM (SELECT 1 AS [X]) Seed
    LEFT JOIN [dbo].[User] AU ON AU.[Id] = @ActorId
    LEFT JOIN [dbo].[User] RU ON RU.[Id] = @RequesterId
    LEFT JOIN [dbo].[Cipher] C ON C.[Id] = @CipherId
    LEFT JOIN [dbo].[Collection] COL ON COL.[Id] = @CollectionId
END
GO

CREATE OR ALTER PROCEDURE [dbo].[AccessAuditEvent_ReadManyByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER,
    @Since DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    -- Reads every event on/after @Since, newest first; rows are self-contained.
    -- Time-derived expiry kinds aren't written by any action yet.
    SELECT
        [Kind],
        [Phase],
        [CorrelationId],
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
