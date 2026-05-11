CREATE OR ALTER PROCEDURE [dbo].[OrganizationReport_GetSummaryDataById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        [OrganizationId],
        [SummaryData],
        [ContentEncryptionKey],
        [RevisionDate]
    FROM [dbo].[OrganizationReportView]
    WHERE [Id] = @Id
END
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationReport_GetReportDataById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        [OrganizationId],
        [ReportData],
        [ContentEncryptionKey],
        [RevisionDate]
    FROM [dbo].[OrganizationReportView]
    WHERE [Id] = @Id
END
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationReport_GetApplicationDataById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        [OrganizationId],
        [ApplicationData],
        [ContentEncryptionKey],
        [RevisionDate]
    FROM [dbo].[OrganizationReportView]
    WHERE [Id] = @Id 
END
GO