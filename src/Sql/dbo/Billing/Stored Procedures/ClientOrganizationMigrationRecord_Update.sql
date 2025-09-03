CREATE PROCEDURE [dbo].[ClientOrganizationMigrationRecord_Update]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @OrganizationId UNIQUEIDENTIFIER,
    @ProviderId UNIQUEIDENTIFIER,
    @PlanType TINYINT,
    @Seats SMALLINT,
    @MaxStorageGb SMALLINT,
    @GatewayCustomerId VARCHAR(50),
    @GatewaySubscriptionId VARCHAR(50),
    @ExpirationDate DATETIME2(7),
    @MaxAutoscaleSeats INT,
    @Status TINYINT
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[ClientOrganizationMigrationRecord]
    SET
        [OrganizationId] = @OrganizationId,
        [ProviderId] = @ProviderId,
        [PlanType] = @PlanType,
        [Seats] = @Seats,
        [MaxStorageGb] = @MaxStorageGb,
        [GatewayCustomerId] = @GatewayCustomerId,
        [GatewaySubscriptionId] = @GatewaySubscriptionId,
        [ExpirationDate] = @ExpirationDate,
        [MaxAutoscaleSeats] = @MaxAutoscaleSeats,
        [Status] = @Status
    WHERE
        [Id] = @Id
END
