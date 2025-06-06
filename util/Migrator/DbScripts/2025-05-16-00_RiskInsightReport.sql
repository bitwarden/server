IF OBJECT_ID('dbo.RiskInsightReport') IS NULL
BEGIN
    CREATE TABLE [dbo].[RiskInsightReport]
    (
    [Id]                       UNIQUEIDENTIFIER NOT NULL,
    [OrganizationId]           UNIQUEIDENTIFIER NOT NULL,
    [Date]                     DATETIME2 (7)    NOT NULL,
    [ReportData]               NVARCHAR(MAX)    NOT NULL,
    [CreationDate]             DATETIME2 (7)    NOT NULL,
    [RevisionDate]             DATETIME2 (7)    NOT NULL,
    CONSTRAINT [PK_RiskInsightReport] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_RiskInsightReport_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id])
    );

    CREATE NONCLUSTERED INDEX [IX_RiskInsightReport_OrganizationId]
        ON [dbo].[RiskInsightReport]([OrganizationId] ASC);
END
GO

IF OBJECT_ID('dbo.RiskInsightReportView') IS NOT NULL
BEGIN
    DROP VIEW [dbo].[RiskInsightReportView]
END
GO

CREATE VIEW [dbo].[RiskInsightReportView] AS
    SELECT * FROM [dbo].[RiskInsightReport];
GO

CREATE OR ALTER PROCEDURE [dbo].[RiskInsightReport_Create]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @Date DATETIME2(7),
    @ReportData NVARCHAR(MAX),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
    SET NOCOUNT ON;
    INSERT INTO [dbo].[RiskInsightReport]( [Id],[OrganizationId],[Date],[ReportData],[CreationDate],[RevisionDate] )
    VALUES ( @Id,@OrganizationId,@Date,@ReportData,@CreationDate,@RevisionDate);
GO

CREATE OR ALTER PROCEDURE [dbo].[RiskInsightReport_ReadByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
    SET NOCOUNT ON;

    IF @OrganizationId IS NULL
        THROW 50000, 'OrganizationId cannot be null', 1;

    SELECT
        [Id],
        [OrganizationId],
        [Date],
        [ReportData],
        [CreationDate],
        [RevisionDate]
    FROM [dbo].[RiskInsightReport]
    WHERE [OrganizationId] = @OrganizationId;
GO

CREATE OR ALTER PROCEDURE [dbo].[RiskInsightReport_ReadById]
    @Id UNIQUEIDENTIFIER
AS
    SET NOCOUNT ON;

    IF @Id IS NULL
            THROW 50000, 'Id cannot be null', 1;

    SELECT
        [Id],
        [OrganizationId],
        [Date],
        [ReportData],
        [CreationDate],
        [RevisionDate]
    FROM [dbo].[RiskInsightReport]
    WHERE [Id] = @Id;
GO

CREATE OR ALTER PROCEDURE [dbo].[RiskInsightReport_Update]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @OrganizationId UNIQUEIDENTIFIER,
    @Date DATETIME2(7),
    @ReportData NVARCHAR(MAX),
    @RevisionDate DATETIME2(7)
AS
    SET NOCOUNT ON;
    UPDATE [dbo].[RiskInsightReport]
    SET [OrganizationId] = @OrganizationId,
        [Date] = @Date,
        [ReportData] = @ReportData,
        [RevisionDate] = @RevisionDate
    WHERE [Id] = @Id;
GO

CREATE OR ALTER PROCEDURE [dbo].[RiskInsightReport_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
    SET NOCOUNT ON;

    IF @Id IS NULL
            THROW 50000, 'Id cannot be null', 1;

    DELETE FROM [dbo].[RiskInsightReport]
    WHERE [Id] = @Id;
GO




