-- Add SubscriptionDiscount_Search stored procedure for pagination
CREATE OR ALTER PROCEDURE [dbo].[SubscriptionDiscount_Search]
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
GO
