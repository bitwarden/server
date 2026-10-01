-- pam/uat only: reshapes an AccessAuditEvent table built by pam/uat's own pre-#8230 scripts into main's shape, so
-- 2026-09-28_00_AddAccessAuditEvent.sql can run over it. A no-op on a fresh database.

IF COL_LENGTH('[dbo].[AccessAuditEvent]', 'OccurredAt') IS NOT NULL
BEGIN
    DROP INDEX IF EXISTS [IX_AccessAuditEvent_OrganizationId_OccurredAt_Id] ON [dbo].[AccessAuditEvent];
    DROP INDEX IF EXISTS [IX_AccessAuditEvent_OrganizationId_OccurredAt] ON [dbo].[AccessAuditEvent];
    EXEC sp_rename '[dbo].[AccessAuditEvent].[OccurredAt]', 'OccurredDate', 'COLUMN';
END
GO

IF COL_LENGTH('[dbo].[AccessAuditEvent]', 'DaemonId') IS NOT NULL
    AND COL_LENGTH('[dbo].[AccessAuditEvent]', 'AccessConnectorId') IS NULL
BEGIN
    EXEC sp_rename '[dbo].[AccessAuditEvent].[DaemonId]', 'AccessConnectorId', 'COLUMN';
END
GO

IF COL_LENGTH('[dbo].[AccessAuditEvent]', 'DaemonName') IS NOT NULL
    AND COL_LENGTH('[dbo].[AccessAuditEvent]', 'AccessConnectorName') IS NULL
BEGIN
    EXEC sp_rename '[dbo].[AccessAuditEvent].[DaemonName]', 'AccessConnectorName', 'COLUMN';
END
GO

IF OBJECT_ID('[dbo].[AccessAuditEvent]') IS NOT NULL
    AND COL_LENGTH('[dbo].[AccessAuditEvent]', 'TargetSystemId') IS NULL
BEGIN
    ALTER TABLE [dbo].[AccessAuditEvent] ADD
        [TargetSystemId]      UNIQUEIDENTIFIER    NULL,
        [TargetSystemName]    NVARCHAR(200)       NULL,
        [AccessConnectorId]   UNIQUEIDENTIFIER    NULL,
        [AccessConnectorName] NVARCHAR(200)       NULL,
        [RotationConfigId]    UNIQUEIDENTIFIER    NULL,
        [RotationJobId]       UNIQUEIDENTIFIER    NULL,
        [RotationSource]      TINYINT             NULL,
        [SyncState]           TINYINT             NULL;
END
GO

IF COL_LENGTH('[dbo].[AccessAuditEvent]', 'CipherName') IS NOT NULL
BEGIN
    ALTER TABLE [dbo].[AccessAuditEvent] DROP COLUMN [CipherName];
END
GO

IF COL_LENGTH('[dbo].[AccessAuditEvent]', 'CollectionName') IS NOT NULL
BEGIN
    ALTER TABLE [dbo].[AccessAuditEvent] DROP COLUMN [CollectionName];
END
GO

IF OBJECT_ID('[dbo].[DF_AccessAuditEvent_CorrelationId]', 'D') IS NOT NULL
BEGIN
    ALTER TABLE [dbo].[AccessAuditEvent] DROP CONSTRAINT [DF_AccessAuditEvent_CorrelationId];
END
GO
