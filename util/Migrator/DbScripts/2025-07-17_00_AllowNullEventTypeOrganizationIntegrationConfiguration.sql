ALTER TABLE [dbo].[OrganizationIntegrationConfiguration]
    ALTER COLUMN [EventType] SMALLINT NULL
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationIntegrationConfiguration_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @OrganizationIntegrationId UNIQUEIDENTIFIER,
    @Configuration VARCHAR(MAX),
    @Template VARCHAR(MAX),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @Filters VARCHAR(MAX) = NULL,
    @EventType SMALLINT = NULL
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[OrganizationIntegrationConfiguration]
        (
        [Id],
        [OrganizationIntegrationId],
        [Configuration],
        [Template],
        [CreationDate],
        [RevisionDate],
        [Filters],
        [EventType]
        )
    VALUES
        (
            @Id,
            @OrganizationIntegrationId,
            @Configuration,
            @Template,
            @CreationDate,
            @RevisionDate,
            @Filters,
            @EventType
        )
END
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationIntegrationConfiguration_Update]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @OrganizationIntegrationId UNIQUEIDENTIFIER,
    @Configuration VARCHAR(MAX),
    @Template VARCHAR(MAX),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @Filters VARCHAR(MAX) = NULL,
    @EventType SMALLINT = NULL
AS
BEGIN
    SET NOCOUNT ON

UPDATE
    [dbo].[OrganizationIntegrationConfiguration]
SET
    [OrganizationIntegrationId] = @OrganizationIntegrationId,
    [Configuration] = @Configuration,
    [Template] = @Template,
    [CreationDate] = @CreationDate,
    [RevisionDate] = @RevisionDate,
    [Filters] = @Filters,
    [EventType] = @EventType
WHERE
    [Id] = @Id
END
GO
