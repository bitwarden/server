IF OBJECT_ID('dbo.OrganizationReport') IS NULL 
BEGIN
    CREATE TABLE [dbo].[OrganizationReport]
    (
        [Id] UNIQUEIDENTIFIER NOT NULL,
        [OrganizationId] UNIQUEIDENTIFIER NOT NULL,
        [Date] DATETIME2 (7) NOT NULL,
        [ReportData] NVARCHAR(MAX) NOT NULL,
        [CreationDate] DATETIME2 (7) NOT NULL,
        CONSTRAINT [PK_OrganizationReport] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_OrganizationReport_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id])
    );

    CREATE NONCLUSTERED INDEX [IX_OrganizationReport_OrganizationId] ON [dbo].[OrganizationReport]([OrganizationId] ASC);

    CREATE NONCLUSTERED INDEX [IX_OrganizationReport_OrganizationId_Date] ON [dbo].[OrganizationReport]([OrganizationId] ASC, [Date] DESC);

END
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
    @Date DATETIME2(7),
    @ReportData NVARCHAR(MAX),
    @CreationDate DATETIME2(7)
AS
    SET NOCOUNT ON;

    INSERT INTO [dbo].[OrganizationReport]( 
        [Id],
        [OrganizationId],
        [Date],
        [ReportData],
        [CreationDate]
    )
    VALUES ( 
        @Id,
        @OrganizationId,
        @Date,
        @ReportData,
        @CreationDate
    );
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationReport_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
    SET NOCOUNT ON;

    DELETE FROM [dbo].[OrganizationReport]
    WHERE [Id] = @Id;

GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationReport_ReadById]
    @Id UNIQUEIDENTIFIER
AS
    SET NOCOUNT ON;

    SELECT
        *
    FROM [dbo].[OrganizationReportView]
    WHERE [Id] = @Id;

GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationReport_ReadByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
    SET NOCOUNT ON;

    SELECT
        *
    FROM [dbo].[OrganizationReportView]
    WHERE [OrganizationId] = @OrganizationId;
GO
