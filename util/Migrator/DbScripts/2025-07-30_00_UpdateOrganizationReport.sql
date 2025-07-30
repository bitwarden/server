DROP INDEX [IX_OrganizationReport_OrganizationId_Date] ON [dbo].[OrganizationReport];
GO


IF OBJECT_ID('dbo.OrganizationReport') IS NOT NULL
BEGIN
ALTER TABLE [dbo].[OrganizationReport]
    DROP COLUMN [Date];
END
GO

IF OBJECT_ID('dbo.OrganizationReport') IS NOT NULL
BEGIN
ALTER TABLE [dbo].[OrganizationReport]
    ADD [SummaryData] NVARCHAR(MAX) NOT NULL,
        [ApplicationData] NVARCHAR(MAX) NOT NULL,
        [RevisionDate] DATETIME2 (7) NOT NULL;
END
GO


CREATE NONCLUSTERED INDEX [IX_OrganizationReport_OrganizationId_CreationDate]
    ON [dbo].[OrganizationReport]([OrganizationId] ASC, [CreationDate] DESC);
GO


CREATE OR ALTER VIEW [dbo].[OrganizationReportView]
AS
SELECT
    *
FROM
    [dbo].[OrganizationReport];
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationReport_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @OrganizationId UNIQUEIDENTIFIER,
    @SummaryData NVARCHAR(MAX),
    @ReportData NVARCHAR(MAX),
    @ApplicationData NVARCHAR(MAX),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @ContentEncryptionKey VARCHAR(MAX)
AS
    SET NOCOUNT ON;

INSERT INTO [dbo].[OrganizationReport](
    [Id],
    [OrganizationId],
    [SummaryData],
    [ReportData],
    [ApplicationData],
    [CreationDate],
    [RevisionDate],
    [ContentEncryptionKey]
)
VALUES (
    @Id,
    @OrganizationId,
    @SummaryData,
    @ReportData,
    @ApplicationData,
    @CreationDate,
    @RevisionDate,
    @ContentEncryptionKey
);
GO
