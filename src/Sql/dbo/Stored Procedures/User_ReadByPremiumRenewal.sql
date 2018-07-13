CREATE PROCEDURE [dbo].[User_ReadByPremiumRenewal]
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @WindowRef DATETIME2(7) = GETUTCDATE()
    DECLARE @WindowStart DATETIME2(7) = DATEADD (day, -15, @WindowRef)
    DECLARE @WindowEnd DATETIME2(7) = DATEADD (day, 15, @WindowRef)

    SELECT
        *
    FROM
        [dbo].[UserView]
    WHERE
        [Premium] = 1
        AND [PremiumExpirationDate] >= @WindowRef
        AND [PremiumExpirationDate] < @WindowEnd
        AND (
            [RenewalReminderDate] IS NULL
            OR [RenewalReminderDate] < @WindowStart
        )
        AND [Gateway] = 1 -- Braintree
END