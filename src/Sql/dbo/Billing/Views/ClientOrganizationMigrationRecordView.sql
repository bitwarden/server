CREATE VIEW [dbo].[ClientOrganizationMigrationRecordView]
AS
SELECT
    [Id],
    [OrganizationId],
    [ProviderId],
    [PlanType],
    [Seats],
    COALESCE([MaxStorageGbIncreased], [MaxStorageGb]) AS [MaxStorageGb],
    [GatewayCustomerId],
    [GatewaySubscriptionId],
    [ExpirationDate],
    [MaxAutoscaleSeats],
    [Status],
    [MaxStorageGbIncreased]
FROM
    [dbo].[ClientOrganizationMigrationRecord]
