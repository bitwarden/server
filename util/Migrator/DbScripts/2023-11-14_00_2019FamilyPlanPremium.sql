UPDATE
    [dbo].[Organization]
SET
    [UsersGetPremium] = 0
WHERE
    [PlanType] = 1 -- Families 2019 Annual
