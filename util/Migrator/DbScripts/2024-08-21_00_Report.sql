-- Report

-- Table
IF OBJECT_ID('[dbo].[Report]') IS NULL
BEGIN
    CREATE TABLE [dbo].[Report]
    (
        [Id]             UNIQUEIDENTIFIER NOT NULL,
        [OrganizationId] UNIQUEIDENTIFIER NOT NULL,
        [Name]           NVARCHAR(MAX)    NULL,
        [GroupId]        UNIQUEIDENTIFIER NULL,
        [Type]           TINYINT          NOT NULL,
        [Parameters]     NVARCHAR(MAX)    NOT NULL,
        [CreationDate]   DATETIME2(7),
        [RevisionDate]   DATETIME2(7),
        CONSTRAINT [PK_Report] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_Report_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id]),
        CONSTRAINT [FK_Report_Group] FOREIGN KEY ([GroupId]) REFERENCES [dbo].[Group] ([Id])
    );
END
GO

-- Create indexes
IF NOT EXISTS(SELECT name FROM sys.indexes WHERE name = 'IX_Report_OrganizationId')
    BEGIN
        CREATE NONCLUSTERED INDEX [IX_Report_OrganizationId]
            ON [dbo].[Report]([OrganizationId] ASC);
    END
GO

IF NOT EXISTS(SELECT name FROM sys.indexes WHERE name = 'IX_Report_Group')
    BEGIN
        CREATE NONCLUSTERED INDEX [IX_Report_Group]
            ON [dbo].[Report]([GroupId] ASC)
            WHERE [GroupId] IS NOT NULL;
    END
GO

-- Stored Procedure: Create
IF OBJECT_ID('[dbo].[Report_Create]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Report_Create]
END
GO

CREATE PROCEDURE [dbo].[Report_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @OrganizationId UNIQUEIDENTIFIER,
    @Name NVARCHAR(MAX),
    @GroupId UNIQUEIDENTIFIER,
    @Type TINYINT,
    @Parameters NVARCHAR(MAX),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[Report]
    (
        [Id],
        [OrganizationId],
        [Name],
        [GroupId],
        [Type],
        [Parameters],
        [CreationDate],
        [RevisionDate]
    )
    VALUES
        (
            @Id,
            @OrganizationId,
            @Name,
            @GroupId,
            @Type,
            @Parameters,
            @CreationDate,
            @RevisionDate
        )
END
GO

-- Stored Procedure: Update
IF OBJECT_ID('[dbo].[Report_Update]') IS NOT NULL
    BEGIN
        DROP PROCEDURE [dbo].[Report_Update]
    END
GO

CREATE PROCEDURE [dbo].[Report_Update]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @Name NVARCHAR(MAX),
    @GroupId UNIQUEIDENTIFIER,
    @Type TINYINT,
    @Parameters NVARCHAR(MAX),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[Report]
    SET
        [OrganizationId] = @OrganizationId,
        [Name] = @Name,
        [GroupId] = @GroupId,
        [Type] = @Type,
        [Parameters] = @Parameters,
        [CreationDate] = @CreationDate,
        [RevisionDate] = @RevisionDate
    WHERE
        [Id] = @Id
END
GO

-- Stored Procedure: ReadByOrganizationId
IF OBJECT_ID('[dbo].[Report_ReadByOrganizationId]') IS NOT NULL
    BEGIN
        DROP PROCEDURE [dbo].[Report_ReadByOrganizationId]
    END
GO

CREATE PROCEDURE [dbo].[Report_ReadByOrganizationId]
@OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[Report]
    WHERE
        [OrganizationId] = @OrganizationId
END
GO
