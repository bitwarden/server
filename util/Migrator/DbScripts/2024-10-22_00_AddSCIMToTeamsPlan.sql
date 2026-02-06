SET DEADLOCK_PRIORITY HIGH
GO
UPDATE
    [dbo].[Organization]
SET
    [UseScim] = 1
WHERE
    [PlanType] IN (
        17, -- Teams (Monthly)
        18 -- Teams (Annually)
    )
SET DEADLOCK_PRIORITY NORMAL
GO
