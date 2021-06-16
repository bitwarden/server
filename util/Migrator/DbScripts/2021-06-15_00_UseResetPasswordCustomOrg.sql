-- For Enterprise (annual/monthly) and Custom (internal) orgs, enable potential use of UseResetPassword
UPDATE
    [dbo].[Organization]
SET
    [UseResetPassword] = (CASE WHEN [PlanType] = 10 OR [PlanType] = 11 OR [PlanType] = 6 THEN 1 ELSE 0 END)
GO