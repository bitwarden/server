-- ========================================
-- Dependency Validation
-- ========================================
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('[dbo].[OrganizationUser]')
    AND name = 'RevocationReason'
)
BEGIN
    RAISERROR('RevocationReason column does not exist on OrganizationUser. The 2026-04-13_00 migration must be deployed first.', 16, 1);
    RETURN;
END;
GO

-- ========================================
-- Temporary filtered index
-- ========================================
-- A filtered index over (Status = -1 AND RevocationReason IS NULL) lets the batched
-- UPDATE seek directly to the rows that still need work. The index shrinks as rows
-- fall out of the filter, so each batch stays cheap.
IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID('dbo.OrganizationUser')
    AND name = 'IX_TEMP_OrganizationUser_RevokedNoReason'
)
BEGIN
    PRINT 'Creating temporary filtered index on OrganizationUser...';
    CREATE INDEX IX_TEMP_OrganizationUser_RevokedNoReason
        ON [dbo].[OrganizationUser]([Id])
        WHERE [Status] = -1 AND [RevocationReason] IS NULL;
    PRINT 'Temporary index created.';
END
GO

-- ========================================
-- Backfill RevocationReason = 0 (Unknown) for revoked users
-- ========================================
-- Sets RevocationReason to Unknown (0) for every revoked user that pre-dates the
-- revocation-reason feature. See RevocationReason enum: Unknown is explicitly
-- defined as "migrated from before revocation reasons were tracked."
DECLARE @BatchSize INT = 2000;
DECLARE @RowsAffected INT = 1;

WHILE @RowsAffected > 0
BEGIN
    UPDATE TOP (@BatchSize) [dbo].[OrganizationUser]
    SET [RevocationReason] = 0  -- RevocationReason.Unknown
    WHERE [Status] = -1          -- OrganizationUserStatusType.Revoked
      AND [RevocationReason] IS NULL;

    SET @RowsAffected = @@ROWCOUNT;
    WAITFOR DELAY '00:00:00.100'
END

GO

-- ========================================
-- Drop temporary index
-- ========================================
DROP INDEX IF EXISTS [IX_TEMP_OrganizationUser_RevokedNoReason] ON [dbo].[OrganizationUser];
PRINT 'Temporary index dropped.';
GO
