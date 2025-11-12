CREATE PROCEDURE [dbo].[ClientOrganizationMigrationRecord_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @OrganizationId UNIQUEIDENTIFIER,
    @ProviderId UNIQUEIDENTIFIER,
    @PlanType TINYINT,
    @Seats SMALLINT,
    @MaxStorageGb SMALLINT,
    @MaxStorageGbIncreased SMALLINT = NULL,
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
        [MaxStorageGbIncreased],
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
        @MaxStorageGbIncreased,
        @GatewayCustomerId,
        @GatewaySubscriptionId,
        @ExpirationDate,
        @MaxAutoscaleSeats,
        @Status
    )
END
