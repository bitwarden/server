IF OBJECT_ID('[dbo].[SsoConfig]') IS NULL
BEGIN
    CREATE TABLE [dbo].[SsoConfig] (
        [Id]                 BIGINT              IDENTITY (1, 1) NOT NULL,
        [Enabled]            BIT                 NOT NULL,
        [OrganizationId]     UNIQUEIDENTIFIER    NOT NULL,
        [Data]               NVARCHAR (MAX)      NULL,
        [CreationDate]       DATETIME2 (7)       NOT NULL,
        [RevisionDate]       DATETIME2 (7)       NOT NULL,
        CONSTRAINT [PK_SsoConfig] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_SsoConfig_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id]) 
    );
END
GO

IF EXISTS(SELECT * FROM sys.views WHERE [Name] = 'SsoConfigView')
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

IF OBJECT_ID('[dbo].[SsoConfig_Create]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[SsoConfig_Create]
END
GO

CREATE PROCEDURE [dbo].[SsoConfig_Create]
    @Id BIGINT OUTPUT,
    @Enabled BIT,
    @OrganizationId UNIQUEIDENTIFIER,
    @Data NVARCHAR(MAX),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[SsoConfig]
    (
        [Enabled],
        [OrganizationId],
        [Data],
        [CreationDate],
        [RevisionDate]
    )
    VALUES
    (
        @Enabled,
        @OrganizationId,
        @Data,
        @CreationDate,
        @RevisionDate
    )

    SET @Id = SCOPE_IDENTITY();
END
GO

IF OBJECT_ID('[dbo].[SsoConfig_Update]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[SsoConfig_Update]
END
GO

CREATE PROCEDURE [dbo].[SsoConfig_Update]
    @Id BIGINT,
    @Enabled BIT,
    @OrganizationId UNIQUEIDENTIFIER,
    @Data NVARCHAR(MAX),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[SsoConfig]
    SET
        [Enabled] = @Enabled,
        [OrganizationId] = @OrganizationId,
        [Data] = @Data,
        [CreationDate] = @CreationDate,
        [RevisionDate] = @RevisionDate
    WHERE
        [Id] = @Id
END
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
        SSO.*
    FROM
        [dbo].[SsoConfigView] SSO
    INNER JOIN
        [dbo].[Organization] O ON O.[Id] = SSO.[OrganizationId]
        AND O.[Identifier] = @Identifier
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
