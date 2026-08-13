-- Add [ChurnDiscountAppliedDate] as an included column on the cohort assignment lookup index.
-- This eliminates a key lookup when the cohort counts queries read churn-redemption state,
-- improving performance of OrganizationPlanMigrationCohort_ReadManyWithCountsByName and
-- OrganizationPlanMigrationCohortAssignment_ReadNonPendingCountByCohortId.
IF EXISTS (
    SELECT
        NULL
    FROM
        sys.indexes
    WHERE
        [name] = 'IX_OrganizationPlanMigrationCohortAssignment_CohortId_ScheduledDate_MigratedDate'
        AND object_id = OBJECT_ID('[dbo].[OrganizationPlanMigrationCohortAssignment]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_OrganizationPlanMigrationCohortAssignment_CohortId_ScheduledDate_MigratedDate]
    ON [dbo].[OrganizationPlanMigrationCohortAssignment] ([CohortId] ASC, [ScheduledDate] ASC, [MigratedDate] ASC)
    INCLUDE ([ChurnDiscountAppliedDate])
    WITH (DROP_EXISTING = ON);
END
ELSE
BEGIN
    CREATE NONCLUSTERED INDEX [IX_OrganizationPlanMigrationCohortAssignment_CohortId_ScheduledDate_MigratedDate]
    ON [dbo].[OrganizationPlanMigrationCohortAssignment] ([CohortId] ASC, [ScheduledDate] ASC, [MigratedDate] ASC)
    INCLUDE ([ChurnDiscountAppliedDate]);
END
GO
