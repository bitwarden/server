CREATE PROCEDURE [dbo].[OrganizationIntegrationConfiguration_Create]
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
