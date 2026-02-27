-- Remove old Type migration entry if it was previously applied
IF EXISTS (
    SELECT 1 FROM [dbo].[Migration]
    WHERE [Filename] = '2026-02-27_00_OrganizationReport_AddType'
)
BEGIN
    DELETE FROM [dbo].[Migration]
    WHERE [Filename] = '2026-02-27_00_OrganizationReport_AddType';
END
GO

-- Drop Type column if it exists
IF EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[dbo].[OrganizationReport]')
    AND name = 'Type'
)
BEGIN
    -- Drop the default constraint first
    DECLARE @ConstraintName NVARCHAR(256);
    SELECT @ConstraintName = dc.name
    FROM sys.default_constraints dc
    JOIN sys.columns c ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
    WHERE c.object_id = OBJECT_ID(N'[dbo].[OrganizationReport]')
    AND c.name = 'Type';

    IF @ConstraintName IS NOT NULL
    BEGIN
        EXEC('ALTER TABLE [dbo].[OrganizationReport] DROP CONSTRAINT [' + @ConstraintName + ']');
    END

    ALTER TABLE [dbo].[OrganizationReport]
    DROP COLUMN [Type];
END
GO

-- Add ReportFile column if it does not exist
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[dbo].[OrganizationReport]')
    AND name = 'ReportFile'
)
BEGIN
    ALTER TABLE [dbo].[OrganizationReport]
    ADD [ReportFile] NVARCHAR(MAX) NOT NULL DEFAULT '';
END
GO

-- Refresh view metadata after table modification
EXEC sp_refreshview N'[dbo].[OrganizationReportView]';
GO

-- Update OrganizationReport_Create to include ReportFile
CREATE OR ALTER PROCEDURE [dbo].[OrganizationReport_Create]
   @Id UNIQUEIDENTIFIER OUTPUT,
   @OrganizationId UNIQUEIDENTIFIER,
   @ReportData NVARCHAR(MAX),
   @CreationDate DATETIME2(7),
   @ContentEncryptionKey VARCHAR(MAX),
   @SummaryData NVARCHAR(MAX),
   @ApplicationData NVARCHAR(MAX),
   @RevisionDate DATETIME2(7),
   @ApplicationCount INT = NULL,
   @ApplicationAtRiskCount INT = NULL,
   @CriticalApplicationCount INT = NULL,
   @CriticalApplicationAtRiskCount INT = NULL,
   @MemberCount INT = NULL,
   @MemberAtRiskCount INT = NULL,
   @CriticalMemberCount INT = NULL,
   @CriticalMemberAtRiskCount INT = NULL,
   @PasswordCount INT = NULL,
   @PasswordAtRiskCount INT = NULL,
   @CriticalPasswordCount INT = NULL,
   @CriticalPasswordAtRiskCount INT = NULL,
   @ReportFile NVARCHAR(MAX) = ''
AS
BEGIN
   SET NOCOUNT ON;

INSERT INTO [dbo].[OrganizationReport](
    [Id],
    [OrganizationId],
    [ReportData],
    [CreationDate],
    [ContentEncryptionKey],
    [SummaryData],
    [ApplicationData],
    [RevisionDate],
    [ApplicationCount],
    [ApplicationAtRiskCount],
    [CriticalApplicationCount],
    [CriticalApplicationAtRiskCount],
    [MemberCount],
    [MemberAtRiskCount],
    [CriticalMemberCount],
    [CriticalMemberAtRiskCount],
    [PasswordCount],
    [PasswordAtRiskCount],
    [CriticalPasswordCount],
    [CriticalPasswordAtRiskCount],
    [ReportFile]
)
VALUES (
    @Id,
    @OrganizationId,
    @ReportData,
    @CreationDate,
    @ContentEncryptionKey,
    @SummaryData,
    @ApplicationData,
    @RevisionDate,
    @ApplicationCount,
    @ApplicationAtRiskCount,
    @CriticalApplicationCount,
    @CriticalApplicationAtRiskCount,
    @MemberCount,
    @MemberAtRiskCount,
    @CriticalMemberCount,
    @CriticalMemberAtRiskCount,
    @PasswordCount,
    @PasswordAtRiskCount,
    @CriticalPasswordCount,
    @CriticalPasswordAtRiskCount,
    @ReportFile
    );
END
GO

-- Update OrganizationReport_Update to include ReportFile
CREATE OR ALTER PROCEDURE [dbo].[OrganizationReport_Update]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @ReportData NVARCHAR(MAX),
    @CreationDate DATETIME2(7),
    @ContentEncryptionKey VARCHAR(MAX),
    @SummaryData NVARCHAR(MAX),
    @ApplicationData NVARCHAR(MAX),
    @RevisionDate DATETIME2(7),
    @ApplicationCount INT = NULL,
    @ApplicationAtRiskCount INT = NULL,
    @CriticalApplicationCount INT = NULL,
    @CriticalApplicationAtRiskCount INT = NULL,
    @MemberCount INT = NULL,
    @MemberAtRiskCount INT = NULL,
    @CriticalMemberCount INT = NULL,
    @CriticalMemberAtRiskCount INT = NULL,
    @PasswordCount INT = NULL,
    @PasswordAtRiskCount INT = NULL,
    @CriticalPasswordCount INT = NULL,
    @CriticalPasswordAtRiskCount INT = NULL,
    @ReportFile NVARCHAR(MAX) = ''
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE [dbo].[OrganizationReport]
    SET
        [OrganizationId] = @OrganizationId,
        [ReportData] = @ReportData,
        [CreationDate] = @CreationDate,
        [ContentEncryptionKey] = @ContentEncryptionKey,
        [SummaryData] = @SummaryData,
        [ApplicationData] = @ApplicationData,
        [RevisionDate] = @RevisionDate,
        [ApplicationCount] = @ApplicationCount,
        [ApplicationAtRiskCount] = @ApplicationAtRiskCount,
        [CriticalApplicationCount] = @CriticalApplicationCount,
        [CriticalApplicationAtRiskCount] = @CriticalApplicationAtRiskCount,
        [MemberCount] = @MemberCount,
        [MemberAtRiskCount] = @MemberAtRiskCount,
        [CriticalMemberCount] = @CriticalMemberCount,
        [CriticalMemberAtRiskCount] = @CriticalMemberAtRiskCount,
        [PasswordCount] = @PasswordCount,
        [PasswordAtRiskCount] = @PasswordAtRiskCount,
        [CriticalPasswordCount] = @CriticalPasswordCount,
        [CriticalPasswordAtRiskCount] = @CriticalPasswordAtRiskCount,
        [ReportFile] = @ReportFile
    WHERE [Id] = @Id;
END;
GO
