CREATE PROCEDURE [dbo].[OrganizationIntegrationConfiguration_Update]
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
