-- OrganizationIntegration

-- Table
IF OBJECT_ID('[dbo].[OrganizationIntegration]') IS NULL
BEGIN
    CREATE TABLE [dbo].[OrganizationIntegration]
    (
        [Id] UNIQUEIDENTIFIER NOT NULL,
        [OrganizationId] UNIQUEIDENTIFIER NOT NULL,
        [Type] SMALLINT NOT NULL,
        [Configuration] VARCHAR (MAX) NULL,
        [CreationDate] DATETIME2 (7) NOT NULL,
        [RevisionDate] DATETIME2 (7) NOT NULL,
        CONSTRAINT [PK_OrganizationIntegration] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_OrganizationIntegration_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id])
    );

    CREATE NONCLUSTERED INDEX [IX_OrganizationIntegration_OrganizationId]
    ON [dbo].[OrganizationIntegration]([OrganizationId] ASC);

    CREATE UNIQUE INDEX [IX_OrganizationIntegration_Organization_Type]
    ON [dbo].[OrganizationIntegration]([OrganizationId], [Type]);
END
GO

-- View
IF EXISTS(SELECT *
FROM sys.views
WHERE [Name] = 'OrganizationIntegrationView')
BEGIN
    DROP VIEW [dbo].[OrganizationIntegrationView];
END
GO

CREATE VIEW [dbo].[OrganizationIntegrationView]
AS
    SELECT
        *
    FROM
        [dbo].[OrganizationIntegration]
GO

-- OrganizationIntegrationConfiguration

-- Table
IF OBJECT_ID('[dbo].[OrganizationIntegrationConfiguration]') IS NULL
BEGIN
    CREATE TABLE [dbo].[OrganizationIntegrationConfiguration]
    (
        [Id] UNIQUEIDENTIFIER NOT NULL,
        [OrganizationIntegrationId] UNIQUEIDENTIFIER NOT NULL,
        [EventType] SMALLINT NOT NULL,
        [Configuration] VARCHAR (MAX) NULL,
        [Template] VARCHAR (MAX) NULL,
        [CreationDate] DATETIME2 (7) NOT NULL,
        [RevisionDate] DATETIME2 (7) NOT NULL,
        CONSTRAINT [PK_OrganizationIntegrationConfiguration] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_OrganizationIntegrationConfiguration_OrganizationIntegration] FOREIGN KEY ([OrganizationIntegrationId]) REFERENCES [dbo].[OrganizationIntegration] ([Id])
    );
END
GO

-- View
IF EXISTS(SELECT *
FROM sys.views
WHERE [Name] = 'OrganizationIntegrationConfigurationView')
BEGIN
    DROP VIEW [dbo].[OrganizationIntegrationConfigurationView];
END
GO

CREATE VIEW [dbo].[OrganizationIntegrationConfigurationView]
AS
    SELECT
        *
    FROM
        [dbo].[OrganizationIntegrationConfiguration]
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationIntegrationConfiguration_ReadManyByEventTypeOrganizationIdIntegrationType]
    @EventType SMALLINT,
    @OrganizationId UNIQUEIDENTIFIER,
    @IntegrationType SMALLINT
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        oic.*
    FROM
        [dbo].[OrganizationIntegrationConfigurationView] oic
        INNER JOIN
        [dbo].[OrganizationIntegration] oi ON oi.[Id] = oic.[OrganizationIntegrationId]
    WHERE
        oic.[EventType] = @EventType
        AND
        oi.[OrganizationId] = @OrganizationId
        AND
        oi.[Type] = @IntegrationType
END
GO
