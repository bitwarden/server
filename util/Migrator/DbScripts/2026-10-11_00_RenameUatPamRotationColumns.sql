-- Renames pam/uat's rotation columns before main's 2026-10-12_00_AddPamRotation.sql runs, for a UAT database that
-- predates 2026-10-15_01_ReshapeUatPamRotation.sql: main's indexes and procedures need the new names. A no-op on a
-- fresh database (no tables yet) or one that already ran the reshape. The rest of the reshape stays in 2026-10-15_01.

-- A column a filtered index's WHERE clause references can't be renamed (error 5074); main's script recreates it.
IF EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'IX_PamRotationConfig_NextRotationAt'
    AND [object_id] = OBJECT_ID('[dbo].[PamRotationConfig]'))
BEGIN
    DROP INDEX [IX_PamRotationConfig_NextRotationAt] ON [dbo].[PamRotationConfig]
END
GO

IF COL_LENGTH('[dbo].[PamAccessConnector]', 'LastHeartbeatAt') IS NOT NULL
    AND COL_LENGTH('[dbo].[PamAccessConnector]', 'LastHeartbeatDate') IS NULL
BEGIN
    EXEC sp_rename N'[dbo].[PamAccessConnector].[LastHeartbeatAt]', N'LastHeartbeatDate', N'COLUMN'
END
GO

IF COL_LENGTH('[dbo].[PamRotationConfig]', 'NextRotationAt') IS NOT NULL
    AND COL_LENGTH('[dbo].[PamRotationConfig]', 'NextRotationDate') IS NULL
BEGIN
    EXEC sp_rename N'[dbo].[PamRotationConfig].[NextRotationAt]', N'NextRotationDate', N'COLUMN'
END
GO

IF COL_LENGTH('[dbo].[PamRotationConfig]', 'LastRotationAt') IS NOT NULL
    AND COL_LENGTH('[dbo].[PamRotationConfig]', 'LastRotationDate') IS NULL
BEGIN
    EXEC sp_rename N'[dbo].[PamRotationConfig].[LastRotationAt]', N'LastRotationDate', N'COLUMN'
END
GO

IF COL_LENGTH('[dbo].[PamRotationJob]', 'ClaimedAt') IS NOT NULL
    AND COL_LENGTH('[dbo].[PamRotationJob]', 'ClaimedDate') IS NULL
BEGIN
    EXEC sp_rename N'[dbo].[PamRotationJob].[ClaimedAt]', N'ClaimedDate', N'COLUMN'
END
GO

IF COL_LENGTH('[dbo].[PamRotationJob]', 'NextClaimableAt') IS NOT NULL
    AND COL_LENGTH('[dbo].[PamRotationJob]', 'NextClaimableDate') IS NULL
BEGIN
    EXEC sp_rename N'[dbo].[PamRotationJob].[NextClaimableAt]', N'NextClaimableDate', N'COLUMN'
END
GO

IF COL_LENGTH('[dbo].[PamRotationJob]', 'ExpiresAt') IS NOT NULL
    AND COL_LENGTH('[dbo].[PamRotationJob]', 'ExpirationDate') IS NULL
BEGIN
    EXEC sp_rename N'[dbo].[PamRotationJob].[ExpiresAt]', N'ExpirationDate', N'COLUMN'
END
GO

-- Renamed rather than left for main's script, which would add a second index beside it.
IF EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'IX_PamRotationJob_Action_ExpiresAt'
    AND [object_id] = OBJECT_ID('[dbo].[PamRotationJob]'))
BEGIN
    EXEC sp_rename N'[dbo].[PamRotationJob].[IX_PamRotationJob_Action_ExpiresAt]', N'IX_PamRotationJob_Action_ExpirationDate', N'INDEX'
END
GO
