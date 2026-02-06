CREATE PROCEDURE [dbo].[ProviderPlan_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @ProviderId UNIQUEIDENTIFIER,
    @PlanType TINYINT,
    @SeatMinimum INT,
    @PurchasedSeats INT,
    @AllocatedSeats INT
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[ProviderPlan]
    (
        [Id],
        [ProviderId],
        [PlanType],
        [SeatMinimum],
        [PurchasedSeats],
        [AllocatedSeats]
    )
    VALUES
    (
        @Id,
        @ProviderId,
        @PlanType,
        @SeatMinimum,
        @PurchasedSeats,
        @AllocatedSeats
    )
END
