
IF OBJECT_ID('dbo.PasswordHealthReportApplication') IS NULL
BEGIN
    CREATE TABLE [dbo].[PasswordHealthReportApplication] 
    (
        Id UNIQUEIDENTIFIER NOT NULL,
        OrganizationId UNIQUEIDENTIFIER NOT NULL,
        Uri nvarchar(max),
        CreationDate   DATETIME2(7)     NOT NULL,
        RevisionDate   DATETIME2(7)     NOT NULL,
        CONSTRAINT [PK_PasswordHealthReportApplication] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_PasswordHealthReportApplication_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id]),
    );

    CREATE NONCLUSTERED INDEX [IX_PasswordHealthReportApplication_OrganizationId]
            ON [dbo].[PasswordHealthReportApplication] (OrganizationId);
END
GO

IF OBJECT_ID('dbo.PasswordHealthReportApplicationView') IS NOT NULL
BEGIN
    DROP VIEW [dbo].[PasswordHealthReportApplicationView]
END
GO

CREATE VIEW [dbo].[PasswordHealthReportApplicationView] AS
    SELECT * FROM [dbo].[PasswordHealthReportApplication]
GO

CREATE OR ALTER PROC dbo.PasswordHealthReportApplication_Create
    @Id UNIQUEIDENTIFIER OUTPUT,
    @OrganizationId UNIQUEIDENTIFIER,
    @Uri nvarchar(max),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
    SET NOCOUNT ON;
    INSERT INTO dbo.PasswordHealthReportApplication ( Id, OrganizationId, Uri, CreationDate, RevisionDate ) 
    VALUES ( @Id, @OrganizationId, @Uri, @CreationDate, @RevisionDate )
GO

CREATE OR ALTER PROC dbo.PasswordHealthReportApplication_ReadByOrganizationId
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
    FROM [dbo].[PasswordHealthReportApplicationView]
    WHERE OrganizationId = @OrganizationId;
GO

CREATE OR ALTER PROC dbo.PasswordHealthReportApplication_ReadById
    @Id UNIQUEIDENTIFIER
AS
    SET NOCOUNT ON;
    
    IF @Id IS NULL
        THROW 50000, 'Id cannot be null', 1;
        
    SELECT 
        Id,
        OrganizationId,
        Uri,
        CreationDate,
        RevisionDate
    FROM [dbo].[PasswordHealthReportApplicationView]
    WHERE Id = @Id;
GO

CREATE OR ALTER PROC dbo.PasswordHealthReportApplication_Update
    @Id UNIQUEIDENTIFIER OUTPUT,
    @OrganizationId UNIQUEIDENTIFIER,
    @Uri nvarchar(max),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
    SET NOCOUNT ON;
    UPDATE dbo.PasswordHealthReportApplication 
        SET OrganizationId = @OrganizationId, 
            Uri = @Uri, 
            RevisionDate = @RevisionDate
    WHERE Id = @Id
GO

CREATE OR ALTER PROC dbo.PasswordHealthReportApplication_DeleteById
    @Id UNIQUEIDENTIFIER
AS
    SET NOCOUNT ON;

    IF @Id IS NULL
        THROW 50000, 'Id cannot be null', 1;

    DELETE FROM [dbo].[PasswordHealthReportApplication]
    WHERE [Id] = @Id
GO