CREATE PROCEDURE [dbo].[SubscriptionDiscount_ReadActive]
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[SubscriptionDiscountView]
    WHERE
        [StartDate] <= GETUTCDATE()
        AND [EndDate] >= GETUTCDATE()
END
