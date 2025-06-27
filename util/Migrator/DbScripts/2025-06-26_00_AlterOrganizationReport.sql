IF COL_LENGTH('[dbo].[OrganizationReport]', 'ReportKey') IS NULL
BEGIN
    ALTER TABLE [dbo].[OrganizationReport]
    ADD [ReportKey] NVARCHAR(MAX) NOT NULL;
END
GO

CREATE OR ALTER VIEW [dbo].[OrganizationReportView]
AS
SELECT
    *
FROM
    [dbo].[OrganizationReport]
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationReport_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @OrganizationId UNIQUEIDENTIFIER,
    @Date DATETIME2(7),
    @ReportData NVARCHAR(MAX),
    @CreationDate DATETIME2(7),
    @ReportKey NVARCHAR(MAX)
AS
    SET NOCOUNT ON;

    INSERT INTO [dbo].[OrganizationReport]
    (
        [Id],
        [OrganizationId],
        [Date],
        [ReportData],
        [CreationDate],
        [ReportKey]
    )
    VALUES
    (
        @Id,
        @OrganizationId,
        @Date,
        @ReportData,
        @CreationDate,
        @ReportKey
    );
GO
