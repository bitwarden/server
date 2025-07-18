IF EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[dbo].[OrganizationIntegrationConfiguration]')
    AND name = 'EventType'
    AND is_nullable = 0 -- Currently NOT NULL
)
BEGIN
    ALTER TABLE [dbo].[OrganizationIntegrationConfiguration]
        ALTER COLUMN [EventType] SMALLINT NULL
END
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationIntegrationConfiguration_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @OrganizationIntegrationId UNIQUEIDENTIFIER,
    @EventType SMALLINT,
    @Configuration VARCHAR(MAX),
    @Template VARCHAR(MAX),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @Filters VARCHAR(MAX) = NULL
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
        [Filters]
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
            @Filters
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
    @Filters VARCHAR(MAX) = NULL
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
    [Filters] = @Filters
WHERE
    [Id] = @Id
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
    oi.[OrganizationId],
    oi.[Type] AS [IntegrationType],
    oic.[EventType],
    oic.[Configuration],
    oi.[Configuration] AS [IntegrationConfiguration],
    oic.[Template],
    oic.[Filters]
FROM
    [dbo].[OrganizationIntegrationConfiguration] oic
        INNER JOIN
    [dbo].[OrganizationIntegration] oi ON oi.[Id] = oic.[OrganizationIntegrationId]
GO
