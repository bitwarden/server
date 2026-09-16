IF COL_LENGTH('[dbo].[OrganizationIntegration]', 'DisabledDate') IS NULL
BEGIN
    ALTER TABLE [dbo].[OrganizationIntegration]
        ADD [DisabledDate] DATETIME2(7) NULL;
END
GO

IF COL_LENGTH('[dbo].[OrganizationIntegration]', 'DisabledReason') IS NULL
BEGIN
    ALTER TABLE [dbo].[OrganizationIntegration]
        ADD [DisabledReason] INT NULL;
END
GO

CREATE OR ALTER VIEW [dbo].[OrganizationIntegrationView]
AS
    SELECT
        *
    FROM
        [dbo].[OrganizationIntegration]
GO

CREATE OR ALTER VIEW [dbo].[OrganizationIntegrationConfigurationDetailsView]
AS
    SELECT
        oi.[OrganizationId],
        oi.[Type] AS [IntegrationType],
        oic.[EventType],
        oic.[Configuration],
        oi.[Configuration] AS [IntegrationConfiguration],
        oic.[Template],
        oic.[Filters],
        oi.[DisabledDate]
    FROM
        [dbo].[OrganizationIntegrationConfiguration] oic
        INNER JOIN
        [dbo].[OrganizationIntegration] oi ON oi.[Id] = oic.[OrganizationIntegrationId]
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationIntegration_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @OrganizationId UNIQUEIDENTIFIER,
    @Type SMALLINT,
    @Configuration VARCHAR(MAX),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @DisabledDate DATETIME2(7) = NULL,
    @DisabledReason INT = NULL
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[OrganizationIntegration]
        (
        [Id],
        [OrganizationId],
        [Type],
        [Configuration],
        [CreationDate],
        [RevisionDate],
        [DisabledDate],
        [DisabledReason]
        )
    VALUES
        (
            @Id,
            @OrganizationId,
            @Type,
            @Configuration,
            @CreationDate,
            @RevisionDate,
            @DisabledDate,
            @DisabledReason
        )
END
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationIntegration_Update]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @OrganizationId UNIQUEIDENTIFIER,
    @Type SMALLINT,
    @Configuration VARCHAR(MAX),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @DisabledDate DATETIME2(7) = NULL,
    @DisabledReason INT = NULL
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[OrganizationIntegration]
    SET
        [OrganizationId] = @OrganizationId,
        [Type] = @Type,
        [Configuration] = @Configuration,
        [CreationDate] = @CreationDate,
        [RevisionDate] = @RevisionDate,
        [DisabledDate] = @DisabledDate,
        [DisabledReason] = @DisabledReason
    WHERE
        [Id] = @Id
END
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationIntegration_Disable]
    @OrganizationId UNIQUEIDENTIFIER,
    @Type SMALLINT,
    @DisabledDate DATETIME2(7),
    @DisabledReason INT,
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    -- Only the first caller wins, so concurrent trips across instances collapse into a single write
    UPDATE
        [dbo].[OrganizationIntegration]
    SET
        [DisabledDate] = @DisabledDate,
        [DisabledReason] = @DisabledReason,
        [RevisionDate] = @RevisionDate
    WHERE
        [OrganizationId] = @OrganizationId
        AND [Type] = @Type
        AND [DisabledDate] IS NULL
END
GO

IF OBJECT_ID('[dbo].[OrganizationIntegration_ReadById]') IS NOT NULL
BEGIN
    EXECUTE sp_refreshsqlmodule N'[dbo].[OrganizationIntegration_ReadById]'
END
GO

IF OBJECT_ID('[dbo].[OrganizationIntegration_ReadManyByOrganizationId]') IS NOT NULL
BEGIN
    EXECUTE sp_refreshsqlmodule N'[dbo].[OrganizationIntegration_ReadManyByOrganizationId]'
END
GO

IF OBJECT_ID('[dbo].[OrganizationIntegration_ReadByTeamsConfigurationTenantIdTeamId]') IS NOT NULL
BEGIN
    EXECUTE sp_refreshsqlmodule N'[dbo].[OrganizationIntegration_ReadByTeamsConfigurationTenantIdTeamId]'
END
GO

IF OBJECT_ID('[dbo].[OrganizationIntegrationConfigurationDetails_ReadMany]') IS NOT NULL
BEGIN
    EXECUTE sp_refreshsqlmodule N'[dbo].[OrganizationIntegrationConfigurationDetails_ReadMany]'
END
GO

IF OBJECT_ID('[dbo].[OrganizationIntegrationConfigurationDetails_ReadManyByEventTypeOrganizationIdIntegrationType]') IS NOT NULL
BEGIN
    EXECUTE sp_refreshsqlmodule N'[dbo].[OrganizationIntegrationConfigurationDetails_ReadManyByEventTypeOrganizationIdIntegrationType]'
END
GO
