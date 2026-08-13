CREATE PROCEDURE [dbo].[SubscriptionDiscount_ReadByStripeCouponId]
    @StripeCouponId VARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[SubscriptionDiscountView]
    WHERE
        [StripeCouponId] = @StripeCouponId
END
