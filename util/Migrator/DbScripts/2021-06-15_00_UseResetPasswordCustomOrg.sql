-- For Enterprise (annual/monthly) and Custom (internal) orgs, enable potential use of UseResetPassword
UPDATE
    [dbo].[Organization]
SET
    [UseResetPassword] = (CASE WHEN [PlanType] IN (10, 11, 6) THEN 1 ELSE [UseResetPassword] END)
GO