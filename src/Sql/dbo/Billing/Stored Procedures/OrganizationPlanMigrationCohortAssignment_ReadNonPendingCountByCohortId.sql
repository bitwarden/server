CREATE PROCEDURE [dbo].[OrganizationPlanMigrationCohortAssignment_ReadNonPendingCountByCohortId]
    @CohortId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        COUNT(1)
    FROM
        [dbo].[OrganizationPlanMigrationCohortAssignmentView] A
    INNER JOIN
        [dbo].[OrganizationPlanMigrationCohortView] C ON C.[Id] = A.[CohortId]
    WHERE
        A.[CohortId] = @CohortId
        AND (
            (C.[MigrationPathId] IS NOT NULL AND (A.[ScheduledDate] IS NOT NULL OR A.[MigratedDate] IS NOT NULL))
            OR
            (C.[MigrationPathId] IS NULL AND A.[ChurnDiscountAppliedDate] IS NOT NULL)
        )
END
