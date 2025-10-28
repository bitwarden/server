IF COL_LENGTH('dbo.OrganizationReport', 'ApplicationCount') IS NULL
BEGIN
ALTER TABLE [dbo].[OrganizationReport]
    ADD [ApplicationCount] INT NULL,
    [ApplicationAtRiskCount] INT NULL,
    [CriticalApplicationCount] INT NULL,
    [CriticalApplicationAtRiskCount] INT NULL,
    [MemberCount] INT NULL,
    [MemberAtRiskCount] INT NULL,
    [CriticalMemberCount] INT NULL,
    [CriticalMemberAtRiskCount] INT NULL,
    [PasswordCount] INT NULL,
    [PasswordAtRiskCount] INT NULL,
    [CriticalPasswordCount] INT NULL,
    [CriticalPasswordAtRiskCount] INT NULL
END
GO

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
    @CriticalPasswordAtRiskCount INT = NULL
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
    [CriticalPasswordAtRiskCount]
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
    @CriticalPasswordAtRiskCount
    );
END
GO

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
    @CriticalPasswordAtRiskCount INT = NULL
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
    [CriticalPasswordAtRiskCount] = @CriticalPasswordAtRiskCount
WHERE [Id] = @Id;
END;
GO

IF OBJECT_ID('dbo.OrganizationReportView') IS NOT NULL
BEGIN
DROP VIEW [dbo].[OrganizationReportView];
END
GO

CREATE VIEW [dbo].[OrganizationReportView]
AS
SELECT
    *
FROM
    [dbo].[OrganizationReport]
    GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationReport_GetLatestByOrganizationId]
@OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

SELECT TOP 1
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
        [CriticalPasswordAtRiskCount]
FROM [dbo].[OrganizationReportView]
WHERE [OrganizationId] = @OrganizationId
ORDER BY [RevisionDate] DESC
END
GO

