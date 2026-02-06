-- OrganizationInstallation

-- Table
IF OBJECT_ID('[dbo].[OrganizationInstallation]') IS NULL
BEGIN
    CREATE TABLE [dbo].[OrganizationInstallation] (
        [Id]             UNIQUEIDENTIFIER NOT NULL,
        [OrganizationId] UNIQUEIDENTIFIER NOT NULL,
        [InstallationId] UNIQUEIDENTIFIER NOT NULL,
        [CreationDate]   DATETIME2 (7) NOT NULL,
        [RevisionDate]   DATETIME2 (7) NULL,
        CONSTRAINT [PK_OrganizationInstallation] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_OrganizationInstallation_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_OrganizationInstallation_Installation] FOREIGN KEY ([InstallationId]) REFERENCES [dbo].[Installation] ([Id]) ON DELETE CASCADE
    );
END
GO

-- Indexes
IF NOT EXISTS(SELECT name FROM sys.indexes WHERE name = 'IX_OrganizationInstallation_OrganizationId')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_OrganizationInstallation_OrganizationId]
        ON [dbo].[OrganizationInstallation]([OrganizationId] ASC);
END

IF NOT EXISTS(SELECT name FROM sys.indexes WHERE name = 'IX_OrganizationInstallation_InstallationId')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_OrganizationInstallation_InstallationId]
        ON [dbo].[OrganizationInstallation]([InstallationId] ASC);
END

-- View
IF EXISTS(SELECT * FROM sys.views WHERE [Name] = 'OrganizationInstallationView')
BEGIN
    DROP VIEW [dbo].[OrganizationInstallationView];
END
GO

CREATE VIEW [dbo].[OrganizationInstallationView]
AS
SELECT
    *
FROM
    [dbo].[OrganizationInstallation]
GO

-- Stored Procedures: Create
CREATE OR ALTER PROCEDURE [dbo].[OrganizationInstallation_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @OrganizationId UNIQUEIDENTIFIER,
    @InstallationId UNIQUEIDENTIFIER,
    @CreationDate DATETIME2 (7),
    @RevisionDate DATETIME2 (7) = NULL
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[OrganizationInstallation]
    (
        [Id],
        [OrganizationId],
        [InstallationId],
        [CreationDate],
        [RevisionDate]
    )
    VALUES
    (
     @Id,
     @OrganizationId,
     @InstallationId,
     @CreationDate,
     @RevisionDate
    )
END
GO

-- Stored Procedures: DeleteById
CREATE OR ALTER PROCEDURE [dbo].[OrganizationInstallation_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DELETE
    FROM
        [dbo].[OrganizationInstallation]
    WHERE
        [Id] = @Id
END
GO

-- Stored Procedures: ReadById
CREATE PROCEDURE [dbo].[OrganizationInstallation_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[OrganizationInstallationView]
    WHERE
        [Id] = @Id
END
GO

-- Stored Procedures: ReadByInstallationId
CREATE PROCEDURE [dbo].[OrganizationInstallation_ReadByInstallationId]
    @InstallationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[OrganizationInstallationView]
    WHERE
        [InstallationId] = @InstallationId
END
GO

-- Stored Procedures: ReadByOrganizationId
CREATE PROCEDURE [dbo].[OrganizationInstallation_ReadByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[OrganizationInstallationView]
    WHERE
        [OrganizationId] = @OrganizationId
END
GO

-- Stored Procedures: Update
CREATE PROCEDURE [dbo].[OrganizationInstallation_Update]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @OrganizationId UNIQUEIDENTIFIER,
    @InstallationId UNIQUEIDENTIFIER,
    @CreationDate DATETIME2 (7),
    @RevisionDate DATETIME2 (7)
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[OrganizationInstallation]
    SET
        [RevisionDate] = @RevisionDate
    WHERE
        [Id] = @Id
END
GO
