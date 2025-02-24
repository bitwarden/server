IF COL_LENGTH('dbo.Organization', 'UseViewPolicies') IS NULL
BEGIN
ALTER TABLE
    [dbo].[Organization]
    ADD
    [UseViewPolicies] BIT NOT NULL DEFAULT(0)
END
GO

UPDATE [dbo].[Organization]
    SET [UseViewPolicies] = 0
    WHERE [PlanType] IN(2,3,4,5,8,9,10,11,12,13,14,15,17,18,19,20);
