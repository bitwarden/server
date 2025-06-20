/* add new column "Filters", nullable to OrganizationIntegrationConfiguration */

IF COL_LENGTH('[dbo].[OrganizationIntegrationConfiguration]', 'Filters') IS NULL
    BEGIN
        ALTER TABLE
            [dbo].[OrganizationIntegrationConfiguration]
            ADD
                [Filters] VARCHAR (MAX) NULL
    END
GO

/* add column "Filters" to OrganizationIntegrationConfigurationView */
CREATE OR ALTER VIEW [dbo].[OrganizationIntegrationConfigurationDetailsView]
AS
SELECT
    oi.[OrganizationId],
    oi.[Type] AS [IntegrationType],
    oic.[EventType],
    oic.[Filters],
    oic.[Configuration],
    oi.[Configuration] AS [IntegrationConfiguration],
    oic.[Template]
FROM
    [dbo].[OrganizationIntegrationConfiguration] oic
        INNER JOIN
    [dbo].[OrganizationIntegration] oi ON oi.[Id] = oic.[OrganizationIntegrationId]
GO

/* add column to OrganizationIntegrationConfiguration_Create */
CREATE OR ALTER PROCEDURE [dbo].[OrganizationIntegrationConfiguration_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @OrganizationIntegrationId UNIQUEIDENTIFIER,
    @EventType SMALLINT,
    @Configuration VARCHAR(MAX),
    @Filters VARCHAR(MAX),
    @Template VARCHAR(MAX),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[OrganizationIntegrationConfiguration]
        (
        [Id],
        [OrganizationIntegrationId],
        [EventType],
        [Configuration],
        [Filters],
        [Template],
        [CreationDate],
        [RevisionDate]
        )
    VALUES
        (
            @Id,
            @OrganizationIntegrationId,
            @EventType,
            @Configuration,
            @Filters,
            @Template,
            @CreationDate,
            @RevisionDate
        )
END
GO

/* add column to OrganizationIntegrationConfiguration_Update */
CREATE OR ALTER PROCEDURE [dbo].[OrganizationIntegrationConfiguration_Update]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @OrganizationIntegrationId UNIQUEIDENTIFIER,
    @EventType SMALLINT,
    @Configuration VARCHAR(MAX),
    @Filters VARCHAR(MAX),
    @Template VARCHAR(MAX),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

UPDATE
    [dbo].[OrganizationIntegrationConfiguration]
SET
    [OrganizationIntegrationId] = @OrganizationIntegrationId,
    [EventType] = @EventType,
    [Configuration] = @Configuration,
    [Filters] = @Filters,
    [Template] = @Template,
    [CreationDate] = @CreationDate,
    [RevisionDate] = @RevisionDate
WHERE
    [Id] = @Id
END
GO
