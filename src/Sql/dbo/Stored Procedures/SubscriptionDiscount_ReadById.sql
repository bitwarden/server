CREATE PROCEDURE [dbo].[SubscriptionDiscount_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[SubscriptionDiscountView]
    WHERE
        [Id] = @Id
END
