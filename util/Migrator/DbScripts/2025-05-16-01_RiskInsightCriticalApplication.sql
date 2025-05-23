IF OBJECT_ID('dbo.RiskInsightCriticalApplication') IS NULL
BEGIN
    CREATE TABLE [dbo].[RiskInsightCriticalApplication] (
        [Id]                       UNIQUEIDENTIFIER NOT NULL,
        [OrganizationId]           UNIQUEIDENTIFIER NOT NULL,
        [Applications]             NVARCHAR(MAX)    NOT NULL,
        [CreationDate]             DATETIME2 (7)    NOT NULL,
        [RevisionDate]             DATETIME2 (7)    NOT NULL,
        CONSTRAINT [PK_RiskInsightCriticalApplication] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_RiskInsightCriticalApplication_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id])
        );

    CREATE NONCLUSTERED INDEX [IX_RiskInsightCriticalApplication_OrganizationId]
        ON [dbo].[RiskInsightCriticalApplication]([OrganizationId] ASC);
END
GO

IF OBJECT_ID('dbo.RiskInsightCriticalApplicationView') IS NOT NULL
BEGIN
    DROP VIEW [dbo].[RiskInsightCriticalApplicationView];
END
GO

CREATE VIEW [dbo].[RiskInsightCriticalApplicationView] AS
    SELECT * FROM [dbo].[RiskInsightCriticalApplication];
GO

CREATE OR ALTER PROCEDURE [dbo].[RiskInsightCriticalApplication_Create]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @Applications NVARCHAR(MAX),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
    SET NOCOUNT ON;

    INSERT INTO [dbo].[RiskInsightCriticalApplication]
    (
        [Id],
        [OrganizationId],
        [Applications],
        [CreationDate],
        [RevisionDate]
    )
    VALUES
        (
        @Id,
        @OrganizationId,
        @Applications,
        @CreationDate,
        @RevisionDate
        );
GO

CREATE OR ALTER PROCEDURE [dbo].[RiskInsightCriticalApplication_ReadByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
    SET NOCOUNT ON;

    IF @OrganizationId IS NULL
       THROW 50000, 'OrganizationId cannot be null', 1;

    SELECT
        [Id],
        [OrganizationId],
        [Applications],
        [CreationDate],
        [RevisionDate]
    FROM [dbo].[RiskInsightCriticalApplication]
    WHERE [OrganizationId] = @OrganizationId;
GO

CREATE OR ALTER PROCEDURE [dbo].[RiskInsightCriticalApplication_ReadById]
    @Id UNIQUEIDENTIFIER
AS
    SET NOCOUNT ON;

    IF @Id IS NULL
       THROW 50000, 'Id cannot be null', 1;

    SELECT
        [Id],
        [OrganizationId],
        [Applications],
        [CreationDate],
        [RevisionDate]
    FROM [dbo].[RiskInsightCriticalApplication]
    WHERE [Id] = @Id;
GO

CREATE OR ALTER PROCEDURE [dbo].[RiskInsightCriticalApplication_Update]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @Applications NVARCHAR(MAX),
    @RevisionDate DATETIME2(7)
AS
    SET NOCOUNT ON;
    UPDATE [dbo].[RiskInsightCriticalApplication]
    SET
        [OrganizationId] = @OrganizationId,
        [Applications] = @Applications,
        [RevisionDate] = @RevisionDate
    WHERE [Id] = @Id;
GO

CREATE OR ALTER PROCEDURE [dbo].[RiskInsightCriticalApplication_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
    SET NOCOUNT ON;

    IF @Id IS NULL
       THROW 50000, 'Id cannot be null', 1;

    DELETE FROM [dbo].[RiskInsightCriticalApplication]
    WHERE [Id] = @Id;
GO










