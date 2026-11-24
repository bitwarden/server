-- pam/uat only. The UAT database still holds the PAM tables pam/uat's own scripts built before 2026-09-22.
-- Main's scripts skip a table that exists and then fail on its new columns, so drop the old-shape tables and
-- let main's scripts create them. Their rows are test data. A no-op on a database without the old shape.

IF COL_LENGTH('[dbo].[AccessAuditEvent]', 'OccurredAt') IS NOT NULL
BEGIN
    DROP TABLE [dbo].[AccessAuditEvent]
END
GO

IF OBJECT_ID('[dbo].[PamDaemon]') IS NOT NULL
BEGIN
    -- The daemons' credentials go too; each daemon registers again as an access connector.
    SELECT [ApiKeyId] INTO #OldDaemonApiKey FROM [dbo].[PamDaemon]

    DROP TABLE IF EXISTS [dbo].[PamRotationAttempt]
    DROP TABLE IF EXISTS [dbo].[PamRotationJob]
    DROP TABLE IF EXISTS [dbo].[PamRotationConfig]
    DROP TABLE IF EXISTS [dbo].[PamDaemonTargetAssignment]
    DROP TABLE IF EXISTS [dbo].[PamTargetSystem]
    DROP TABLE IF EXISTS [dbo].[PamLeaseExpirySweep]
    DROP TABLE [dbo].[PamDaemon]

    DELETE FROM [dbo].[ApiKey] WHERE [Id] IN (SELECT [ApiKeyId] FROM #OldDaemonApiKey)
    DROP TABLE #OldDaemonApiKey

    -- Duplicates main's IX_AccessLease_CipherId_Action_NotAfter.
    DROP INDEX IF EXISTS [IX_AccessLease_CipherId_Action] ON [dbo].[AccessLease]

    -- Procedures and views main's scripts never create.
    DECLARE @Drop NVARCHAR(MAX) = N''
    SELECT @Drop += N'DROP ' + CASE [type] WHEN 'V' THEN N'VIEW ' ELSE N'PROCEDURE ' END
        + QUOTENAME(SCHEMA_NAME([schema_id])) + N'.' + QUOTENAME([name]) + N';'
    FROM sys.objects
    WHERE [type] IN ('P', 'V') AND ([name] LIKE 'PamDaemon%' OR [name] IN (
        'AccessLease_ExpireDue',
        'PamRotationAttempt_AcceptCipherWrite',
        'PamRotationAttempt_MarkErrored',
        'PamRotationAttempt_MarkRotated',
        'PamRotationConfig_AnyByTargetSystem',
        'PamRotationConfig_AnyByTargetSystemWithTerminateSessions',
        'PamRotationJob_Claim',
        'PamRotationJob_ReadManyClaimableByDaemonId',
        'PamRotationJob_ReadManyRecentByDaemonId',
        'PamRotationJob_ReleaseExpiredLeases',
        'PamRotationJob_TimeoutDue'))
    EXEC sp_executesql @Drop
END
GO
