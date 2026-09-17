IF COL_LENGTH('[dbo].[OrganizationIntegrationConfiguration]', 'DisabledDate') IS NULL
BEGIN
    ALTER TABLE [dbo].[OrganizationIntegrationConfiguration]
        ADD [DisabledDate] DATETIME2(7) NULL;
END
GO

IF COL_LENGTH('[dbo].[OrganizationIntegrationConfiguration]', 'DisabledReason') IS NULL
BEGIN
    ALTER TABLE [dbo].[OrganizationIntegrationConfiguration]
        ADD [DisabledReason] INT NULL;
END
GO

CREATE OR ALTER VIEW [dbo].[OrganizationIntegrationConfigurationView]
AS
    SELECT
        *
    FROM
        [dbo].[OrganizationIntegrationConfiguration]
GO

CREATE OR ALTER VIEW [dbo].[OrganizationIntegrationConfigurationDetailsView]
AS
    SELECT
        oic.[Id],
        oi.[Id] AS [OrganizationIntegrationId],
        oi.[OrganizationId],
        oi.[Type] AS [IntegrationType],
        oic.[EventType],
        oic.[Configuration],
        oi.[Configuration] AS [IntegrationConfiguration],
        oic.[Template],
        oic.[Filters],
        oic.[DisabledDate]
    FROM
        [dbo].[OrganizationIntegrationConfiguration] oic
        INNER JOIN
        [dbo].[OrganizationIntegration] oi ON oi.[Id] = oic.[OrganizationIntegrationId]
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationIntegrationConfiguration_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @OrganizationIntegrationId UNIQUEIDENTIFIER,
    @EventType SMALLINT,
    @Configuration VARCHAR(MAX),
    @Template VARCHAR(MAX),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @Filters VARCHAR(MAX) = NULL,
    @DisabledDate DATETIME2(7) = NULL,
    @DisabledReason INT = NULL
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[OrganizationIntegrationConfiguration]
        (
        [Id],
        [OrganizationIntegrationId],
        [EventType],
        [Configuration],
        [Template],
        [CreationDate],
        [RevisionDate],
        [Filters],
        [DisabledDate],
        [DisabledReason]
        )
    VALUES
        (
            @Id,
            @OrganizationIntegrationId,
            @EventType,
            @Configuration,
            @Template,
            @CreationDate,
            @RevisionDate,
            @Filters,
            @DisabledDate,
            @DisabledReason
        )
END
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationIntegrationConfiguration_Update]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @OrganizationIntegrationId UNIQUEIDENTIFIER,
    @EventType SMALLINT,
    @Configuration VARCHAR(MAX),
    @Template VARCHAR(MAX),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @Filters VARCHAR(MAX) = NULL,
    @DisabledDate DATETIME2(7) = NULL,
    @DisabledReason INT = NULL
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[OrganizationIntegrationConfiguration]
    SET
        [OrganizationIntegrationId] = @OrganizationIntegrationId,
        [EventType] = @EventType,
        [Configuration] = @Configuration,
        [Template] = @Template,
        [CreationDate] = @CreationDate,
        [RevisionDate] = @RevisionDate,
        [Filters] = @Filters,
        [DisabledDate] = @DisabledDate,
        [DisabledReason] = @DisabledReason
    WHERE
        [Id] = @Id
END
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationIntegrationConfiguration_Disable]
    @OrganizationId UNIQUEIDENTIFIER,
    @Id UNIQUEIDENTIFIER,
    @DisabledDate DATETIME2(7),
    @DisabledReason INT,
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    -- Scoped through the integration so the write cannot cross tenants. Only the first caller wins, so concurrent
    -- trips across instances collapse into a single write.
    UPDATE
        oic
    SET
        oic.[DisabledDate] = @DisabledDate,
        oic.[DisabledReason] = @DisabledReason,
        oic.[RevisionDate] = @RevisionDate
    FROM
        [dbo].[OrganizationIntegrationConfiguration] oic
        INNER JOIN
        [dbo].[OrganizationIntegration] oi ON oi.[Id] = oic.[OrganizationIntegrationId]
    WHERE
        oic.[Id] = @Id
        AND oic.[DisabledDate] IS NULL
        AND oi.[OrganizationId] = @OrganizationId

    -- Returned explicitly because SET NOCOUNT ON suppresses the row count ExecuteNonQuery would report
    SELECT @@ROWCOUNT
END
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationIntegrationConfiguration_ClearDisabledByIntegrationId]
    @OrganizationIntegrationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[OrganizationIntegrationConfiguration]
    SET
        [DisabledDate] = NULL,
        [DisabledReason] = NULL
    WHERE
        [OrganizationIntegrationId] = @OrganizationIntegrationId
        AND [DisabledDate] IS NOT NULL
END
GO

IF OBJECT_ID('[dbo].[OrganizationIntegrationConfiguration_ReadById]') IS NOT NULL
BEGIN
    EXECUTE sp_refreshsqlmodule N'[dbo].[OrganizationIntegrationConfiguration_ReadById]'
END
GO

IF OBJECT_ID('[dbo].[OrganizationIntegrationConfiguration_ReadManyByOrganizationIntegrationId]') IS NOT NULL
BEGIN
    EXECUTE sp_refreshsqlmodule N'[dbo].[OrganizationIntegrationConfiguration_ReadManyByOrganizationIntegrationId]'
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
