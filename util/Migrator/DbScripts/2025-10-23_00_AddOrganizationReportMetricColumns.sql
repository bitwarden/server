IF OBJECT_ID('dbo.OrganizationReport') IS NOT NULL
BEGIN
    ALTER TABLE [dbo].[OrganizationReport]
        ADD [ApplicationCount] INT NULL,
            [CriticalApplicationCount] INT NULL,
            [ApplicationAtRiskCount] INT NULL,
            [CriticalApplicationAtRiskCount] INT NULL,
            [PasswordAtRiskCount] INT NULL,
            [CriticalPasswordAtRiskCount] INT NULL,
            [MemberAtRiskCount] INT NULL,
            [CriticalMemberAtRiskCount] INT NULL,
            [CriticalMemberCount] INT NULL;
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
   @ApplicationCount INT,
   @CriticalApplicationCount INT,
   @ApplicationAtRiskCount INT,
   @CriticalApplicationAtRiskCount INT,
   @PasswordAtRiskCount INT,
   @CriticalPasswordAtRiskCount INT,
   @MemberAtRiskCount INT,
   @CriticalMemberAtRiskCount INT,
   @CriticalMemberCount INT
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
    [CriticalApplicationCount],
    [ApplicationAtRiskCount],
    [CriticalApplicationAtRiskCount],
    [PasswordAtRiskCount],
    [CriticalPasswordAtRiskCount],
    [MemberAtRiskCount],
    [CriticalMemberAtRiskCount],
    [CriticalMemberCount]
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
    @CriticalApplicationCount,
    @ApplicationAtRiskCount,
    @CriticalApplicationAtRiskCount,
    @PasswordAtRiskCount,
    @CriticalPasswordAtRiskCount,
    @MemberAtRiskCount,
    @CriticalMemberAtRiskCount,
    @CriticalMemberCount
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
    @ApplicationCount INT,
    @CriticalApplicationCount INT,
    @ApplicationAtRiskCount INT,
    @CriticalApplicationAtRiskCount INT,
    @PasswordAtRiskCount INT,
    @CriticalPasswordAtRiskCount INT,
    @MemberAtRiskCount INT,
    @CriticalMemberAtRiskCount INT,
    @CriticalMemberCount INT
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
        [CriticalApplicationCount] = @CriticalApplicationCount,
        [ApplicationAtRiskCount] = @ApplicationAtRiskCount,
        [CriticalApplicationAtRiskCount] = @CriticalApplicationAtRiskCount,
        [PasswordAtRiskCount] = @PasswordAtRiskCount,
        [CriticalPasswordAtRiskCount] = @CriticalPasswordAtRiskCount,
        [MemberAtRiskCount] = @MemberAtRiskCount,
        [CriticalMemberAtRiskCount] = @CriticalMemberAtRiskCount,
        [CriticalMemberCount] = @CriticalMemberCount
    WHERE [Id] = @Id;
END
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
    SET NOCOUNT ON;

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
        [CriticalApplicationCount],
        [ApplicationAtRiskCount],
        [CriticalApplicationAtRiskCount],
        [PasswordAtRiskCount],
        [CriticalPasswordAtRiskCount],
        [MemberAtRiskCount],
        [CriticalMemberAtRiskCount],
        [CriticalMemberCount]
    FROM [dbo].[OrganizationReportView]
    WHERE [OrganizationId] = @OrganizationId
    ORDER BY [RevisionDate] DESC
END
GO
