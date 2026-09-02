IF OBJECT_ID('dbo.OrganizationApplication') IS NULL
BEGIN
    CREATE TABLE [dbo].[OrganizationApplication] (
        [Id]                       UNIQUEIDENTIFIER NOT NULL,
        [OrganizationId]           UNIQUEIDENTIFIER NOT NULL,
        [Applications]             NVARCHAR(MAX)    NOT NULL,
        [CreationDate]             DATETIME2 (7)    NOT NULL,
        [RevisionDate]             DATETIME2 (7)    NOT NULL,
        CONSTRAINT [PK_OrganizationApplication] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_OrganizationApplication_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id])
        );

    CREATE NONCLUSTERED INDEX [IX_OrganizationApplication_OrganizationId]
        ON [dbo].[OrganizationApplication]([OrganizationId] ASC);
END
GO

CREATE OR ALTER VIEW [dbo].[OrganizationApplicationView] AS
    SELECT * FROM [dbo].[OrganizationApplication];
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationApplication_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @OrganizationId UNIQUEIDENTIFIER,
    @Applications NVARCHAR(MAX),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
    SET NOCOUNT ON;

    INSERT INTO [dbo].[OrganizationApplication]
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

CREATE OR ALTER PROCEDURE [dbo].[OrganizationApplication_ReadByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
    SET NOCOUNT ON;

    SELECT
        *
    FROM [dbo].[OrganizationApplicationView]
    WHERE [OrganizationId] = @OrganizationId;
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationApplication_ReadById]
    @Id UNIQUEIDENTIFIER
AS
    SET NOCOUNT ON;

    SELECT
        *
    FROM [dbo].[OrganizationApplicationView]
    WHERE [Id] = @Id;
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationApplication_Update]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @OrganizationId UNIQUEIDENTIFIER,
    @Applications NVARCHAR(MAX),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
    SET NOCOUNT ON;
    
    UPDATE [dbo].[OrganizationApplication]
    SET
        [OrganizationId] = @OrganizationId,
        [Applications] = @Applications,
        [CreationDate] = @CreationDate,
        [RevisionDate] = @RevisionDate
    WHERE [Id] = @Id;

GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationApplication_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
    SET NOCOUNT ON;

    DELETE FROM [dbo].[OrganizationApplication]
    WHERE [Id] = @Id;
GO
