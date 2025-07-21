CREATE PROCEDURE [dbo].[ClientOrganizationMigrationRecord_Create]
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

    INSERT INTO [dbo].[ClientOrganizationMigrationRecord]
    (
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
    )
    VALUES
    (
        @Id,
        @OrganizationId,
        @ProviderId,
        @PlanType,
        @Seats,
        @MaxStorageGb,
        @GatewayCustomerId,
        @GatewaySubscriptionId,
        @ExpirationDate,
        @MaxAutoscaleSeats,
        @Status
    )
END
