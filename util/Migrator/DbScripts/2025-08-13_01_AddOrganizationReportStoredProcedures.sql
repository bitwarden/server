IF OBJECT_ID('[dbo].[OrganizationReport_GetLatestByOrganizationId]') IS NOT NULL
    DROP PROCEDURE [dbo].[OrganizationReport_GetLatestByOrganizationId]
GO

CREATE PROCEDURE [dbo].[OrganizationReport_GetLatestByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
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

IF OBJECT_ID('[dbo].[OrganizationReport_GetSummariesByDateRange]') IS NOT NULL
    DROP PROCEDURE [dbo].[OrganizationReport_GetSummariesByDateRange]
GO

CREATE PROCEDURE [dbo].[OrganizationReport_GetSummariesByDateRange]
    @OrganizationId UNIQUEIDENTIFIER,
    @Id UNIQUEIDENTIFIER,
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
  AND [Id] = @Id
  AND [RevisionDate] >= @StartDate
  AND [RevisionDate] <= @EndDate
ORDER BY [RevisionDate] DESC
END
GO

IF OBJECT_ID('[dbo].[OrganizationReport_GetSummaryDataById]') IS NOT NULL
    DROP PROCEDURE [dbo].[OrganizationReport_GetSummaryDataById]
GO

CREATE PROCEDURE [dbo].[OrganizationReport_GetSummaryDataById]
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

IF OBJECT_ID('[dbo].[OrganizationReport_UpdateSummaryData]') IS NOT NULL
    DROP PROCEDURE [dbo].[OrganizationReport_UpdateSummaryData]
GO

CREATE PROCEDURE [dbo].[OrganizationReport_UpdateSummaryData]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @SummaryData NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON;

UPDATE [dbo].[OrganizationReport]
SET
    [SummaryData] = @SummaryData,
    [RevisionDate] = GETUTCDATE()
WHERE [Id] = @Id
  AND [OrganizationId] = @OrganizationId;
END
GO

IF OBJECT_ID('[dbo].[OrganizationReport_GetReportDataById]') IS NOT NULL
    DROP PROCEDURE [dbo].[OrganizationReport_GetReportDataById]
GO

CREATE PROCEDURE [dbo].[OrganizationReport_GetReportDataById]
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

IF OBJECT_ID('[dbo].[OrganizationReport_UpdateReportData]') IS NOT NULL
    DROP PROCEDURE [dbo].[OrganizationReport_UpdateReportData]
GO

CREATE PROCEDURE [dbo].[OrganizationReport_UpdateReportData]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @ReportData NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON;

UPDATE [dbo].[OrganizationReport]
SET
    [ReportData] = @ReportData,
    [RevisionDate] = GETUTCDATE()
WHERE [Id] = @Id
  AND [OrganizationId] = @OrganizationId;
END
GO

IF OBJECT_ID('[dbo].[OrganizationReport_GetApplicationDataById]') IS NOT NULL
    DROP PROCEDURE [dbo].[OrganizationReport_GetApplicationDataById]
GO

CREATE PROCEDURE [dbo].[OrganizationReport_GetApplicationDataById]
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
      AND [Id] = @Id
END
GO

IF OBJECT_ID('[dbo].[OrganizationReport_UpdateApplicationData]') IS NOT NULL
    DROP PROCEDURE [dbo].[OrganizationReport_UpdateApplicationData]
GO

CREATE PROCEDURE [dbo].[OrganizationReport_UpdateApplicationData]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @ApplicationData NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE [dbo].[OrganizationReport]
    SET
        [ApplicationData] = @ApplicationData,
        [RevisionDate] = GETUTCDATE()
    WHERE [Id] = @Id
      AND [OrganizationId] = @OrganizationId;
END
GO
