-- Phase 3 (Finalization): Make FileId NOT NULL
-- This runs at the next release after all existing records have FileId values

-- Verify all records have FileId before altering column
IF NOT EXISTS (SELECT 1 FROM [dbo].[OrganizationReport] WHERE [FileId] IS NULL)
BEGIN
    ALTER TABLE [dbo].[OrganizationReport]
    ALTER COLUMN [FileId] VARCHAR(100) NOT NULL;
END
ELSE
BEGIN
    RAISERROR('Cannot make FileId NOT NULL - some records still have NULL FileId', 16, 1);
END
GO

-- Refresh view metadata after table modification
EXEC sp_refreshview N'[dbo].[OrganizationReportView]';
GO
