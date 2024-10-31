
IF OBJECT_ID('dbo.PasswordHealthReportApplications') IS NULL
BEGIN
    CREATE TABLE [dbo].[PasswordHealthReportApplications] 
    (
        Id UNIQUEIDENTIFIER NOT NULL,
        OrganizationId UNIQUEIDENTIFIER NOT NULL,
        Uri nvarchar(max),
        CreationDate   DATETIME2(7)     NOT NULL,
        RevisionDate   DATETIME2(7)     NOT NULL,
        CONSTRAINT [PK_PasswordHealthReportApplications] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_PasswordHealthApplications_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id]),
    );

    CREATE NONCLUSTERED INDEX [IX_PasswordHealthReportApplications_OrganizationId]
            ON [dbo].[PasswordHealthReportApplications] (OrganizationId);
END
GO

IF OBJECT_ID('dbo.PasswordHealthReportApplicationsView') IS NOT NULL
BEGIN
    DROP VIEW [dbo].[PasswordHealthReportApplicationsView]
END
GO

CREATE VIEW [dbo].[PasswordHealthReportApplicationsView] AS
    SET NOCOUNT ON;
    SELECT * FROM [dbo].[PasswordHealthReportApplications]
GO

CREATE OR ALTER PROC dbo.PasswordHealthReportApplications_Create
    @Id UNIQUEIDENTIFIER OUTPUT,
    @OrganizationId UNIQUEIDENTIFIER,
    @Uri nvarchar(max),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
    INSERT INTO dbo.PasswordHealthReportApplications ( Id, OrganizationId, Uri, CreationDate, RevisionDate ) 
    VALUES ( @Id, @OrganizationId, @Uri, @CreationDate, @RevisionDate )
GO

CREATE OR ALTER PROC dbo.PasswordHealthReportApplications_ReadByOrganizationId
    @OrganizationId UNIQUEIDENTIFIER
AS
    SET NOCOUNT ON;
    
    IF @OrganizationId IS NULL
        THROW 50000, 'OrganizationId cannot be null', 1;
        
    SELECT 
        Id,
        OrganizationId,
        Uri,
        CreationDate,
        RevisionDate
    FROM [dbo].[PasswordHealthReportApplicationsView]
    WHERE OrganizationId = @OrganizationId;
GO