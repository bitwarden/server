-- Adds the dedicated PAM audit-event store: a plain append-only table that state-changing PAM actions write to, plus
-- its insert stored procedure. The audit-trail read proc is switched over to read from this table in a later
-- migration. Dapper/MSSQL only for this POC -- there is no EF/self-host track yet (deferred).

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
        CONSTRAINT [PK_AccessAuditEvent] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_AccessAuditEvent_Organization] FOREIGN KEY ([OrganizationId])
            REFERENCES [dbo].[Organization] ([Id]) ON DELETE CASCADE
    );
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE [name] = 'IX_AccessAuditEvent_OrganizationId_OccurredAt'
        AND object_id = OBJECT_ID('[dbo].[AccessAuditEvent]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AccessAuditEvent_OrganizationId_OccurredAt]
        ON [dbo].[AccessAuditEvent] ([OrganizationId] ASC, [OccurredAt] DESC);
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
        [LeaseNotAfter]
    )
    VALUES
    (
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
        @LeaseNotAfter
    )
END
GO
