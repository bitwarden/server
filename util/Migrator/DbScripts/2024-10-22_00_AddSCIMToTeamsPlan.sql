UPDATE
    [dbo].[Organization]
SET
    [UseScim] = 1
WHERE
    [PlanType] IN (
        17, -- Teams (Monthly)
        18 -- Teams (Annually)
    );
