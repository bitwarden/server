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
        [RevisionDate]
    FROM [dbo].[OrganizationReportView]
    WHERE [OrganizationId] = @OrganizationId
    ORDER BY [RevisionDate] DESC
END
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationReport_GetSummariesByDateRange]
    @OrganizationId UNIQUEIDENTIFIER,
    @StartDate DATETIME2(7),
    @EndDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

SELECT
    [SummaryData]
FROM [dbo].[OrganizationReportView]
WHERE [OrganizationId] = @OrganizationId
  AND [RevisionDate] >= @StartDate
  AND [RevisionDate] <= @EndDate
ORDER BY [RevisionDate] DESC
END
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationReport_GetSummaryDataById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

SELECT
    [SummaryData]
FROM [dbo].[OrganizationReportView]
WHERE [Id] = @Id
END
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationReport_UpdateSummaryData]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @SummaryData NVARCHAR(MAX),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON;

UPDATE [dbo].[OrganizationReport]
SET
    [SummaryData] = @SummaryData,
    [RevisionDate] = @RevisionDate
WHERE [Id] = @Id
  AND [OrganizationId] = @OrganizationId;
END
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationReport_GetReportDataById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        [ReportData]
    FROM [dbo].[OrganizationReportView]
    WHERE [Id] = @Id
END
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationReport_UpdateReportData]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @ReportData NVARCHAR(MAX),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON;

UPDATE [dbo].[OrganizationReport]
SET
    [ReportData] = @ReportData,
    [RevisionDate] = @RevisionDate
WHERE [Id] = @Id
  AND [OrganizationId] = @OrganizationId;
END
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationReport_GetApplicationDataById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        [ApplicationData]
    FROM [dbo].[OrganizationReportView]
    WHERE [Id] = @Id;
END
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationReport_UpdateApplicationData]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @ApplicationData NVARCHAR(MAX),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE [dbo].[OrganizationReport]
    SET
        [ApplicationData] = @ApplicationData,
        [RevisionDate] = @RevisionDate
    WHERE [Id] = @Id
      AND [OrganizationId] = @OrganizationId;
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
    @RevisionDate DATETIME2(7)
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
        [RevisionDate] = @RevisionDate
    WHERE [Id] = @Id;
END
GO
