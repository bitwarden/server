IF OBJECT_ID('[dbo].[SsoConfig]') IS NULL
BEGIN
    CREATE TABLE [dbo].[SsoConfig] (
        [OrganizationId]     UNIQUEIDENTIFIER    NULL,
        [Identifier]         NVARCHAR (50)       NULL,
        [Data]               NVARCHAR (MAX)      NULL,
        [CreationDate]       DATETIME2 (7)       NOT NULL,
        [RevisionDate]       DATETIME2 (7)       NOT NULL,
        CONSTRAINT [FK_SsoConfig_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id]) 
    );
END
GO

IF EXISTS(SELECT * FROM sys.views WHERE [Name] = 'SsoConfig')
BEGIN
    DROP VIEW [dbo].[SsoConfigView]
END
GO

CREATE VIEW [dbo].[SsoConfigView]
AS
SELECT
    *
FROM
    [dbo].[SsoConfig]
GO

IF OBJECT_ID('[dbo].[SsoConfig_ReadByIdentifier]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[SsoConfig_ReadByIdentifier]
END
GO

CREATE PROCEDURE [dbo].[SsoConfig_ReadByIdentifier]
    @Identifier NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON

    SELECT TOP 1
        *
    FROM
        [dbo].[SsoConfigView]
    WHERE
            [Identifier] = @Identifier
END
GO

IF OBJECT_ID('[dbo].[SsoConfig_ReadByOrganizationId]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[SsoConfig_ReadByOrganizationId]
END
GO

CREATE PROCEDURE [dbo].[SsoConfig_ReadByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT TOP 1
        *
    FROM
        [dbo].[SsoConfigView]
    WHERE
            [OrganizationId] = @OrganizationId
END
GO
