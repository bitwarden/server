CREATE PROCEDURE [dbo].[SubscriptionDiscount_List]
    @Skip INT = 0,
    @Take INT = 25
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[SubscriptionDiscountView]
    ORDER BY [CreationDate] DESC
    OFFSET @Skip ROWS
    FETCH NEXT @Take ROWS ONLY
END
