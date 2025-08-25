IF EXISTS (
SELECT * FROM sys.indexes WHERE name = 'IX_OrganizationReport_OrganizationId_Date'
AND object_id = OBJECT_ID('dbo.OrganizationReport')
)
BEGIN
    DROP INDEX [IX_OrganizationReport_OrganizationId_Date] ON [dbo].[OrganizationReport];
END
GO

IF COL_LENGTH('[dbo].[OrganizationReport]', 'Date') IS NOT NULL
BEGIN
    ALTER TABLE [dbo].[OrganizationReport]
    DROP COLUMN [Date];
END
GO

IF OBJECT_ID('dbo.OrganizationReport') IS NOT NULL
BEGIN
    ALTER TABLE [dbo].[OrganizationReport]
        ADD [SummaryData] NVARCHAR(MAX) NULL,
        [ApplicationData] NVARCHAR(MAX) NULL,
        [RevisionDate] DATETIME2 (7) NULL;
END
GO

IF NOT EXISTS (
SELECT * FROM sys.indexes WHERE name = 'IX_OrganizationReport_OrganizationId_RevisionDate'
AND object_id = OBJECT_ID('dbo.OrganizationReport')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_OrganizationReport_OrganizationId_RevisionDate]
     ON [dbo].[OrganizationReport]([OrganizationId] ASC, [RevisionDate] DESC);
END
GO

IF OBJECT_ID('dbo.OrganizationReportView') IS NOT NULL
BEGIN
    DROP VIEW [dbo].[OrganizationReportView];
END
GO

IF OBJECT_ID('dbo.OrganizationReportView') IS NULL
BEGIN
    EXEC('CREATE VIEW [dbo].[OrganizationReportView]
    AS
    SELECT
        *
    FROM
        [dbo].[OrganizationReport]');
END
GO

IF OBJECT_ID('dbo.OrganizationReport_Create') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[OrganizationReport_Create];
END
GO

IF OBJECT_ID('dbo.OrganizationReport_Create') IS NULL
BEGIN
    EXEC('CREATE PROCEDURE [dbo].[OrganizationReport_Create]
       @Id UNIQUEIDENTIFIER OUTPUT,
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

    INSERT INTO [dbo].[OrganizationReport](
        [Id],
        [OrganizationId],
        [ReportData],
        [CreationDate],
        [ContentEncryptionKey],
        [SummaryData],
        [ApplicationData],
        [RevisionDate]
    )
    VALUES (
        @Id,
        @OrganizationId,
        @ReportData,
        @CreationDate,
        @ContentEncryptionKey,
        @SummaryData,
        @ApplicationData,
        @RevisionDate
    );
    END');
END
GO
