-- Enable UseMyItems for all Enterprise plan organizations
-- EnterpriseMonthly2019=4, EnterpriseAnnually2019=5
-- EnterpriseMonthly2020=10, EnterpriseAnnually2020=11
-- EnterpriseMonthly2023=14, EnterpriseAnnually2023=15
-- EnterpriseMonthly=19, EnterpriseAnnually=20

UPDATE [dbo].[Organization]
SET [UseMyItems] = 1
WHERE [PlanType] IN (4, 5, 10, 11, 14, 15, 19, 20);
GO
