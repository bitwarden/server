CREATE VIEW [dbo].[ClientOrganizationMigrationRecordView]
AS
SELECT
    [Id],
    [OrganizationId],
    [ProviderId],
    [PlanType],
    [Seats],
    [MaxStorageGb],
    [GatewayCustomerId],
    [GatewaySubscriptionId],
    [ExpirationDate],
    [MaxAutoscaleSeats],
    [Status]
FROM
    [dbo].[ClientOrganizationMigrationRecord]
