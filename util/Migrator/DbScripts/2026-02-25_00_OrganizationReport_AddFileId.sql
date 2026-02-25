IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[dbo].[OrganizationReport]')
    AND name = 'FileId'
)
BEGIN
    ALTER TABLE [dbo].[OrganizationReport]
    ADD [FileId] VARCHAR(100) NULL;
END
GO

-- Update OrganizationReport_Create to include FileId
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
   @FileId VARCHAR(100) = NULL
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
    [FileId]
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
    @FileId
    );
END
GO

-- Update OrganizationReport_Update to include FileId
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
    @FileId VARCHAR(100) = NULL
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
        [FileId] = @FileId
    WHERE [Id] = @Id;
END;
GO

-- Refresh view metadata after table modification
EXEC sp_refreshview N'[dbo].[OrganizationReportView]';
GO
