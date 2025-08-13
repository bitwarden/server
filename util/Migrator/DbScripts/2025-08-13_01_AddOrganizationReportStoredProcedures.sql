IF NOT EXISTS (
   SELECT * FROM sys.objects
   WHERE object_id = OBJECT_ID(N'[dbo].[OrganizationReport_GetLatestByOrganizationId]')
)
CREATE OR ALTER PROCEDURE [dbo].[OrganizationReport_GetLatestByOrganizationId]
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

IF NOT EXISTS (
   SELECT * FROM sys.objects
   WHERE object_id = OBJECT_ID(N'[dbo].[OrganizationReport_GetSummariesByDateRange]')
)
CREATE OR ALTER PROCEDURE [dbo].[OrganizationReport_GetSummariesByDateRange]
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

IF NOT EXISTS (
   SELECT * FROM sys.objects
   WHERE object_id = OBJECT_ID(N'[dbo].[OrganizationReport_GetSummaryDataById]')
)
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

IF NOT EXISTS (
   SELECT * FROM sys.objects
   WHERE object_id = OBJECT_ID(N'[dbo].[OrganizationReport_UpdateSummaryData]')
)
CREATE OR ALTER PROCEDURE [dbo].[OrganizationReport_UpdateSummaryData]
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

IF NOT EXISTS (
   SELECT * FROM sys.objects
   WHERE object_id = OBJECT_ID(N'[dbo].[OrganizationReport_GetReportDataById]')
)
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

IF NOT EXISTS (
   SELECT * FROM sys.objects
   WHERE object_id = OBJECT_ID(N'[dbo].[OrganizationReport_UpdateReportData]')
)
CREATE OR ALTER PROCEDURE [dbo].[OrganizationReport_UpdateReportData]
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

IF NOT EXISTS (
   SELECT * FROM sys.objects
   WHERE object_id = OBJECT_ID(N'[dbo].[OrganizationReport_GetApplicationDataById]')
)
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
      AND [Id] = @Id
END

IF NOT EXISTS (
   SELECT * FROM sys.objects
   WHERE object_id = OBJECT_ID(N'[dbo].[OrganizationReport_UpdateApplicationData]')
)
CREATE OR ALTER PROCEDURE [dbo].[OrganizationReport_UpdateApplicationData]
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



