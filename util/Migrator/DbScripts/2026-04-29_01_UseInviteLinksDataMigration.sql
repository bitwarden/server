-- Enable UseInviteLinks for all Enterprise organizations
-- Enterprise plan types: 4 (EnterpriseMonthly2019), 5 (EnterpriseAnnually2019),
--   10 (EnterpriseMonthly2020), 11 (EnterpriseAnnually2020),
--   14 (EnterpriseMonthly2023), 15 (EnterpriseAnnually2023),
--   19 (EnterpriseMonthly), 20 (EnterpriseAnnually)
-- Batch to avoid table locks
DECLARE @BatchSize INT = 1000;
DECLARE @RowsAffected INT = 1;

WHILE @RowsAffected > 0
    BEGIN
        UPDATE TOP (@BatchSize) [dbo].[Organization]
        SET [UseInviteLinks] = 1
        WHERE [PlanType] IN (4, 5, 10, 11, 14, 15, 19, 20)
          AND [UseInviteLinks] = 0;

        SET @RowsAffected = @@ROWCOUNT;
    END
