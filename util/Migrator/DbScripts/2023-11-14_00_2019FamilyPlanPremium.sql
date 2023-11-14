IF NOT EXISTS(
    SELECT *
    FROM sys.indexes
    WHERE name='IX_Organization_PlanType' AND object_id = OBJECT_ID('[dbo].[Organization]')
)
CREATE NONCLUSTERED INDEX [IX_Organization_PlanType]
ON [dbo].[Organization] ([PlanType])
WITH (ONLINE = ON);

UPDATE
    [dbo].[Organization]
SET
    [UsersGetPremium] = 0
WHERE
    [PlanType] = 1 -- Families 2019 Annual
