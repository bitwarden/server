CREATE PROCEDURE [dbo].[ProviderPlan_Update]
    @Id UNIQUEIDENTIFIER,
    @ProviderId UNIQUEIDENTIFIER,
    @PlanType TINYINT,
    @SeatMinimum INT,
    @PurchasedSeats INT,
    @AllocatedSeats INT
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[ProviderPlan]
    SET
        [ProviderId] = @ProviderId,
        [PlanType] = @PlanType,
        [SeatMinimum] = @SeatMinimum,
        [PurchasedSeats] = @PurchasedSeats,
        [AllocatedSeats] = @AllocatedSeats
    WHERE
        [Id] = @Id
END
