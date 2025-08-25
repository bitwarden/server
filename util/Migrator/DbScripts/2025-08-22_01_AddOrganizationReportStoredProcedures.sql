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
    FROM [dbo].[OrganizationReport]
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
    [Id],
    [OrganizationId],
    [SummaryData]
FROM [dbo].[OrganizationReport]
WHERE [OrganizationId] = @OrganizationId
  AND [RevisionDate] >= @StartDate
  AND [RevisionDate] <= @EndDate
ORDER BY [RevisionDate] DESC
END
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationReport_GetSummaryDataById]
    @OrganizationId UNIQUEIDENTIFIER,
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

SELECT
    [Id],
    [OrganizationId],
    [SummaryData]
FROM [dbo].[OrganizationReport]
WHERE [OrganizationId] = @OrganizationId AND [Id] = @Id
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
    @OrganizationId UNIQUEIDENTIFIER,
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        [Id],
        [OrganizationId],
        [ReportData]
    FROM [dbo].[OrganizationReport]
    WHERE [OrganizationId] = @OrganizationId
      AND [Id] = @Id
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
    @OrganizationId UNIQUEIDENTIFIER,
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        [Id],
        [OrganizationId],
        [ApplicationData]
    FROM [dbo].[OrganizationReport]
    WHERE [OrganizationId] = @OrganizationId
      AND [Id] = @Id;
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
